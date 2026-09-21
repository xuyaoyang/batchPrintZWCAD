using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZwcadBatchPlot;

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        var folder = Path.GetFullPath(args[0]); Directory.CreateDirectory(folder);
        Check(RectangleListOptions.FileName("原图","项目-","-审核",9,1,3,".pdf") == "项目-010-审核.pdf", "prefix suffix start and digit naming");
        Check(RectangleListOptions.FileName("原图","","",1,0,2,".png") == "原图01.png", "empty prefix retains DWG name");
        var large = new PlotJob { SourceFile="a.dwg", SpaceName="Model", MaxX=100,MaxY=100 };
        var small = new PlotJob { SourceFile="a.dwg", SpaceName="Model", MaxX=10,MaxY=10 };
        var other = new PlotJob { SourceFile="b.dwg", SpaceName="Model", MaxX=10,MaxY=10 };
        var layout = new PlotJob { SourceFile="a.dwg", SpaceName="Layout1", MaxX=10,MaxY=10 };
        var frames = new[] { large,small,other,layout };
        var filtered = RectangleListOptions.KeepFrames(frames,2);
        Check(filtered.Count==3 && !filtered.Contains(small) && filtered.Contains(other) && filtered.Contains(layout),"area filter scoped per file and space");
        Check(RectangleListOptions.KeepFrames(frames,1).Count==4,"filter threshold includes equality");
        Check(RectangleListOptions.KeepFrames(frames,0).Count==4,"zero filter restores all frames");
        var source = Path.Combine(folder,"source.png");
        using (var image = new Bitmap(120,60)) { image.SetPixel(60,30,System.Drawing.Color.Red); image.Save(source,System.Drawing.Imaging.ImageFormat.Png); }
        var item = ImageStampLibrary.FromImage(source); item.Name = "测试章";
        var list = new List<ImageStampEntry> { item };
        var library = Path.Combine(folder,"test.lastamps.json"); ImageStampLibrary.Save(library,list);
        File.Delete(source);
        var restored = ImageStampLibrary.Load(library);
        Check(restored.Count == 1 && restored[0].Name == "测试章", "portable library roundtrip after original image removal");
        var png = ImageStampLibrary.Materialize(restored[0],Path.Combine(folder,"images"));
        using (var image = new Bitmap(png)) { Check(image.GetPixel(0,0).A == 0 && image.GetPixel(60,30).R == 255,"transparent and red pixels retained"); }
        Check(ImageStampLibrary.Merge(restored,ImageStampLibrary.Load(library)) == 0,"repeat import deduplicated");
        restored[0].Enabled = false;
        bool blocked = false; try { ImageStampLibrary.Materialize(restored[0],folder); } catch (InvalidOperationException) { blocked = true; }
        Check(blocked,"disabled stamp not selectable");
        ImageStampLibrary.Save(library,restored); Check(!ImageStampLibrary.Load(library)[0].Enabled,"enabled state persisted");
        bool invalid = false; try { ImageStampLibrary.Merge(restored,new[] { new ImageStampEntry { Name = "broken", PngBase64 = "invalid" } }); } catch { invalid = true; }
        Check(invalid && restored.Count == 1,"failed import keeps existing library");
        var dialog = new ImageStampLibraryDialog(true,library);
        dialog.Show(); dialog.UpdateLayout();
        var root = (System.Windows.Controls.DockPanel)dialog.Content;
        var body = (System.Windows.Controls.Grid)root.Children[2];
        var grid = (System.Windows.Controls.DataGrid)body.Children[0];
        Check(grid.Columns[1].ActualWidth >= 150, "stamp name column readable");
        var bitmap = new RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(dialog);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var output = File.Create(Path.Combine(folder,"stamp-library-dialog.png"))) encoder.Save(output);
        dialog.Close(); Check(ImageStampLibrary.Load(library).Count == 1,"dialog close leaves library unchanged");
    }
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS: " + name); }
}
