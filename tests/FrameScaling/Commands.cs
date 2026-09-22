using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
using ZwcadBatchPlot;
public class FrameScalingTests
{
 [CommandMethod("LA_SCALE_CHECK")]
 public void Run()
 {
  var results = new List<object>();
  foreach(var lines in new[]{false,true})
  foreach(var factor in new[]{1.0,0.5,2.0,0.7})
  foreach(var insertion in new[]{1.0,2.0})
  foreach(var angle in new[]{0.0, Math.PI/2})
  {
   try {
    using(var db=new Database(true,true)) {
     Matrix3d transform;
     using(var tr=db.TransactionManager.StartTransaction()) {
      var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);
      var block=new BlockTableRecord{Name="SCALE_FRAME"}; bt.Add(block);tr.AddNewlyCreatedDBObject(block,true);
      var pl=new Polyline();
      double x=30*factor,y=-297*factor;
      pl.AddVertexAt(0,new Point2d(x,y),0,0,0);pl.AddVertexAt(1,new Point2d(x+420*factor,y),0,0,0);
      pl.AddVertexAt(2,new Point2d(x+420*factor,0),0,0,0);pl.AddVertexAt(3,new Point2d(x,0),0,0,0);pl.Closed=true;
      if(lines) { for(int i=0;i<4;i++) { var edge=new Line(pl.GetPoint3dAt(i),pl.GetPoint3dAt((i+1)%4));block.AppendEntity(edge);tr.AddNewlyCreatedDBObject(edge,true); } pl.Dispose(); } else { block.AppendEntity(pl);tr.AddNewlyCreatedDBObject(pl,true); }
      foreach(var pair in new[]{Tuple.Create("TITLE",20.0),Tuple.Create("NUMBER",50.0)}) {
       var t=new DBText{TextString=pair.Item1,Height=3*factor,Position=new Point3d(x+310*factor,y+pair.Item2*factor,0)};
       block.AppendEntity(t);tr.AddNewlyCreatedDBObject(t,true);
      }
      var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
      var br=new BlockReference(new Point3d(1500,2000,0),block.ObjectId){ScaleFactors=new Scale3d(insertion),Rotation=angle};
      ms.AppendEntity(br);tr.AddNewlyCreatedDBObject(br,true);transform=br.BlockTransform;tr.Commit();
     }
     var def=new TitleBlockDefinition{BlockName="SCALE_FRAME",CoordinateMode="Frame",HasPrintRegion=true,
      PrintRegion=LocalRectangle.FromPoints(30,-297,450,0),PaperName="A3",PaperWidthMm=420,PaperHeightMm=297,
      TitleRegion=LocalRectangle.FromPoints(300,10,410,40),DrawingNumberRegion=LocalRectangle.FromPoints(300,40,410,70),StampRegion=LocalRectangle.FromPoints(320,80,400,120)};
     var library=new TitleBlockLibrary();library.Blocks.Add(def);
     var job=TitleBlockScanner.Scan(db,library,"synthetic.dwg").Single();
     var expected=new[]{new Point3d(30*factor,-297*factor,0),new Point3d(450*factor,-297*factor,0),new Point3d(450*factor,0,0),new Point3d(30*factor,0,0)}.Select(p=>p.TransformBy(transform)).ToArray();
     bool boundary=expected.All(p=>Enumerable.Range(0,4).Any(i=>Math.Abs(job.CornerPoints[i*2]-p.X)<0.01 && Math.Abs(job.CornerPoints[i*2+1]-p.Y)<0.01));
     var stamp=new Point3d(350*factor,-217*factor,0).TransformBy(transform);
     bool stampOk=Enumerable.Range(0,4).Any(i=>Math.Abs(job.StampWorldCorners[i*2]-stamp.X)<0.01 && Math.Abs(job.StampWorldCorners[i*2+1]-stamp.Y)<0.01);
     results.Add(new{lines,factor,insertion,angle,boundary,fields=job.Title=="TITLE"&&job.DrawingNumber=="NUMBER",stamp=stampOk,job.Title,job.DrawingNumber});
    }
   } catch(System.Exception ex) {results.Add(new{lines,factor,insertion,angle,error=ex.ToString()});}
  }
  File.WriteAllText(Path.Combine(Path.GetDirectoryName(typeof(FrameScalingTests).Assembly.Location),"scaling-result.json"),Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
 }
}
