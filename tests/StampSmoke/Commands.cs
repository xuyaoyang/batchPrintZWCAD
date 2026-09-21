using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
#if AUTOCAD
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
using App = Autodesk.AutoCAD.ApplicationServices.Application;
using CadDocument = Autodesk.AutoCAD.ApplicationServices.Document;
#else
using ZwSoft.ZwCAD.Runtime;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.DatabaseServices;
using App = ZwSoft.ZwCAD.ApplicationServices.Application;
using CadDocument = ZwSoft.ZwCAD.ApplicationServices.Document;
#endif
using ZwcadBatchPlot;

public class Commands
{
    [CommandMethod("STAMP_VERIFY_RECTANGLE")]
    public void VerifyRectangle() => new BatchPlotCommands().VerifyRectangleBatch();
    static void Check(bool b, string msg) { if (!b) throw new System.Exception(msg); }
    [CommandMethod("STAMP_SMOKE", CommandFlags.Session)]
    public void Run()
    {
        var dir = Path.GetDirectoryName(typeof(Commands).Assembly.Location);
        try
        {
            var doc = App.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                var block = new BlockTableRecord { Name = "STAMP_TEST_FRAME" };
                bt.Add(block); tr.AddNewlyCreatedDBObject(block, true);
                Action<double,double,double,double> rect = (x,y,w,h) => {
                    var pl = new Polyline();
                    pl.AddVertexAt(0, new Point2d(x,y),0,0,0); pl.AddVertexAt(1,new Point2d(x+w,y),0,0,0);
                    pl.AddVertexAt(2,new Point2d(x+w,y+h),0,0,0); pl.AddVertexAt(3,new Point2d(x,y+h),0,0,0);
                    pl.Closed=true; block.AppendEntity(pl); tr.AddNewlyCreatedDBObject(pl,true);
                };
                rect(0,0,420,297); rect(320,20,80,40);
                var text = new DBText { TextString="STAMP TEST ONLY", Height=10, Position=new Point3d(20,180,0) };
                block.AppendEntity(text); tr.AddNewlyCreatedDBObject(text,true);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                for (int i=0;i<5;i++)
                {
                    var br = new BlockReference(new Point3d(i*1200,0,0),block.ObjectId) { ScaleFactors=new Scale3d(i==1?2:1), Rotation=i>=2?(i-1)*Math.PI/2:0 };
                    ms.AppendEntity(br);tr.AddNewlyCreatedDBObject(br,true);
                }
                tr.Commit();
            }
            var def = new TitleBlockDefinition { BlockName="STAMP_TEST_FRAME", HasPrintRegion=true, CoordinateMode="Frame", PrintRegion=LocalRectangle.FromPoints(0,0,420,297), PaperName="A3", PaperWidthMm=420, PaperHeightMm=297, StampRegion=LocalRectangle.FromPoints(320,20,400,60) };
            var libPath=Path.Combine(dir,"library.json");
            TitleBlockLibraryStore.Upsert(def,libPath);
            def.StampRegion=LocalRectangle.FromPoints(320,20,400,60);
            TitleBlockLibraryStore.Upsert(def,libPath);
            var library=TitleBlockLibraryStore.Load(libPath);
            Check(library.Blocks[0].StampRegion.MinX==320,"stamp roundtrip");
            var jobs=TitleBlockScanner.Scan(doc,library);
            Check(jobs.Count==5,"scan 5 blocks");
            Check(jobs.All(j=>j.StampWorldCorners?.Length==8),"stamp coordinates");
            var sourceState=db.Handseed.ToString();
            var settings = new AppSettings { OpenMergedPdfAfterMerge=false, RasterExportDpi=100 };
            var files=new List<string>();
            for(int i=0;i<jobs.Count;i++)
            {
                var job=jobs[i];
                var imageEntry=ImageStampLibrary.FromImage(Path.Combine(dir,"stamp.png"));
                ImageStampLibrary.Save(Path.Combine(dir,"images.lastamps.json"),new[]{imageEntry});
                job.StampImagePath=ImageStampLibrary.Materialize(ImageStampLibrary.Load(Path.Combine(dir,"images.lastamps.json"))[0],Path.Combine(dir,"StampImages"));
                job.OutputPath=Path.Combine(dir,"sheet-"+i+".pdf");
                // Force a visible margin for one sheet to verify custom scale.
                job.LeavePaperMargin=i==1; job.PaperMarginMm=-10;
                var result=PdfRasterExport.PlotMany(new[]{job},AcadPlotterInstaller.PreferredPdfPlotter,"",doc,settings);
                Check(result.Count==1 && result[0].Succeeded,"PDF: "+result.FirstOrDefault()?.Error);
                files.Add(job.OutputPath);
            }
            foreach(var ext in new[]{"png","jpg"})
            {
                var job=jobs[0]; job.OutputPath=Path.Combine(dir,"sheet."+ext);
                var result=PdfRasterExport.PlotMany(new[]{job},AcadPlotterInstaller.PreferredPdfPlotter,"",doc,settings);
                Check(result.Count==1 && result[0].Succeeded,ext+": "+result.FirstOrDefault()?.Error);
            }
            PdfDocumentService.Merge(files,Path.Combine(dir,"merged.pdf"));
            Check(sourceState==db.Handseed.ToString(),"stamp must not add DWG entities");
            // Old template with no configured region remains valid when stamping is disabled.
            var empty=new PlotJob(); ImageStampService.Validate(empty);
            empty.StampImagePath=Path.Combine(dir,"stamp.png");
            bool rejected=false;try { ImageStampService.Validate(empty); } catch { rejected=true; }
            Check(rejected,"unconfigured block should not silently print without stamp");
            if(!System.Diagnostics.Process.GetCurrentProcess().ProcessName.Equals("accoreconsole",StringComparison.OrdinalIgnoreCase)) CaptureForms(doc,dir,def);
            File.WriteAllText(Path.Combine(dir,"result.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,jobs},Newtonsoft.Json.Formatting.Indented));
        }
        catch(System.Exception ex) { File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString()); }
    }

    private static void CaptureForms(CadDocument doc,string dir,TitleBlockDefinition def)
    {
        using(var markers=new TransientFrameMarkers(doc.Editor))
        using(var form=new FieldBoxSelectDialog(doc.Editor,Matrix3d.Identity,Matrix3d.Identity,markers,
            def.PrintRegion, new[]{new PaperDetection {PaperName="A3",PaperWidthMm=420,PaperHeightMm=297,ScaleValue=1}},
            PaperSizeDetector.CreateRectangleBatchOptions(1,false,3,null),
            new FieldBoxSelectInitialState {TitleRegion=LocalRectangle.FromPoints(10,10,20,20),DrawingNumberRegion=LocalRectangle.FromPoints(20,10,30,20),StampRegion=def.StampRegion}))
        {
            Check(form.StampRegion.MinX==320,"field dialog restores stamp");
            form.Shown += (s,e) => form.BeginInvoke(new Action(() => {
                using(var bmp=new System.Drawing.Bitmap(form.Width,form.Height))
                {form.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,0,form.Width,form.Height));bmp.Save(Path.Combine(dir,"field-dialog.png"));}
                form.Close();
            }));
            App.ShowModalDialog(form);
        }
        var window=new BatchPlotForm(doc);
        var content=(System.Windows.FrameworkElement)window.Content;
        ((System.Windows.Controls.Panel)content).Background=window.Background;
        content.Measure(new System.Windows.Size(900,500));content.Arrange(new System.Windows.Rect(0,0,900,500));content.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(900,500,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using(var stream=File.Create(Path.Combine(dir,"batch-dialog.png")))encoder.Save(stream);
        Check(window.FindName("_stampEnabled")!=null,"block batch UI has stamp control");
        var format=(System.Windows.Controls.ComboBox)window.FindName("_outputFormatCombo");
        var stamp=(System.Windows.Controls.CheckBox)window.FindName("_stampEnabled");
        format.SelectedItem="DWF";Check(!stamp.IsEnabled,"DWF has no stamp");
        format.SelectedItem="PDF";Check(stamp.IsEnabled,"PDF has stamp");
        window.Close();
        var rectangle = new RectangleBatchPlotForm(doc);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var rectangleSettings=(AppSettings)typeof(RectangleBatchPlotForm).GetField("_settings",flags).GetValue(rectangle);
        rectangleSettings.RectangleNamePrefix="测试-";rectangleSettings.RectangleNameSuffix="-复核";
        rectangleSettings.RectangleSequenceStart=7;rectangleSettings.RectangleSequenceDigits=3;
        rectangleSettings.RectangleSmallFramePercent=0;
        var jobsForList=new List<RectangleFrameScanner.Result> {
            new RectangleFrameScanner.Result { Job=new PlotJob { SourceFile="fixture.dwg",SpaceName="Model",MaxX=420,MaxY=297,SizeText="420 x 297",ScaleText="1:1" },
                PaperOptions=new[]{new PaperDetection {PaperName="A3",PaperWidthMm=420,PaperHeightMm=297,ScaleValue=1,ScaleText="1:1"}} }
        };
        typeof(RectangleBatchPlotForm).GetMethod("LoadRows",flags).Invoke(rectangle,new object[]{jobsForList,false});
        var grid=(System.Windows.Controls.DataGrid)rectangle.FindName("_grid");
        Check(grid.Columns.Any(c=>c.Header?.ToString()=="图面尺寸"),"rectangle frame dimensions column");
        var listItem=grid.Items[0];
        Check(listItem.GetType().GetProperty("FileName").GetValue(listItem).ToString().StartsWith("测试-007-复核"),"rectangle naming wired to output list");
        var rectangleContent=(System.Windows.FrameworkElement)rectangle.Content;
        ((System.Windows.Controls.Panel)rectangleContent).Background=rectangle.Background;
        rectangleContent.Measure(new System.Windows.Size(1160,530));rectangleContent.Arrange(new System.Windows.Rect(0,0,1160,530));rectangleContent.UpdateLayout();
        var rectBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1160,530,96,96,System.Windows.Media.PixelFormats.Pbgra32);rectBitmap.Render(rectangleContent);
        var rectEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();rectEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rectBitmap));
        using(var stream=File.Create(Path.Combine(dir,"rectangle-dialog.png")))rectEncoder.Save(stream);
        rectangleSettings.RectangleSmallFramePercent=25;
        jobsForList.Add(new RectangleFrameScanner.Result { Job=new PlotJob {SourceFile="fixture.dwg",SpaceName="Model",MaxX=1,MaxY=1,CadTitle="small attribute frame"},PaperOptions=jobsForList[0].PaperOptions});
        typeof(RectangleBatchPlotForm).GetMethod("LoadRows",flags).Invoke(rectangle,new object[]{jobsForList,false});
        Check((bool)typeof(RectangleBatchPlotForm).GetField("_hasAttributeIdentity",flags).GetValue(rectangle),"filter must preserve attribute naming mode for restored frames");
        rectangle.Close();
        var rowType=typeof(TitleBlockLibraryManagerForm).GetNestedType("TitleBlockRow",System.Reflection.BindingFlags.NonPublic);
        var row=rowType.GetMethod("FromDefinition").Invoke(null,new object[]{def});
        var copied=(TitleBlockDefinition)rowType.GetMethod("ToDefinition").Invoke(row,null);
        Check(copied.StampRegion.MinX==320,"library grid save preserves stamp");
    }
}
