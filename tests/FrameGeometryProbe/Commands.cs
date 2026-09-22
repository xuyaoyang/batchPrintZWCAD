using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json;
using ZwcadBatchPlot;
public class GeometryProbe {
 [CommandMethod("LA_GEOMETRY_PROBE")]
 public void Run() {
 var report=new List<object>();var doc=Application.DocumentManager.MdiActiveDocument;var db=doc.Database;
 try {
 var jobs=TitleBlockScanner.Scan(doc,TitleBlockLibraryStore.Load());report.Add(new{jobs});
 using(var tr=db.TransactionManager.StartTransaction()) {
 var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
 foreach(ObjectId bid in bt){var b=(BlockTableRecord)tr.GetObject(bid,OpenMode.ForRead);if(b.Name!="A3"&&b.Name!="A4")continue;
 var entities=new List<object>();foreach(ObjectId eid in b){try{var e=(Entity)tr.GetObject(eid,OpenMode.ForRead);entities.Add(new{type=e.GetType().Name,handle=e.Handle.ToString(),layer=e.Layer,ext=e.GeometricExtents.ToString(),points=e is Polyline p?Enumerable.Range(0,p.NumberOfVertices).Select(i=>p.GetPoint2dAt(i).ToString()).ToArray():null});}catch{}}
 var type=typeof(TitleBlockScanner).Assembly.GetType("ZwcadBatchPlot.BlockFrameGeometry");var m=type.GetMethods(BindingFlags.NonPublic|BindingFlags.Static).Single(x=>x.Name=="TryGetFrame"&&x.GetParameters()[0].ParameterType==typeof(Transaction));var a=new object[]{tr,bid,null,null};m.Invoke(null,a);
 report.Add(new{name=b.Name,frame=a[2],source=a[3].ToString(),entities});
 }
 }
 }catch(System.Exception ex){report.Add(new{error=ex.ToString()});}
 File.WriteAllText(Path.Combine(Path.GetDirectoryName(typeof(GeometryProbe).Assembly.Location),"geometry.json"),JsonConvert.SerializeObject(report,Formatting.Indented));
 }
}
