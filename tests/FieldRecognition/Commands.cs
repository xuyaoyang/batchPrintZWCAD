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
public class FieldProbe {
 #if FIELD_FIX
 [CommandMethod("LA_FIELD_CHECK_FIXED")]
#else
 [CommandMethod("LA_FIELD_CHECK")]
#endif
 public void Run() {
 var report=new List<object>(); var doc=Application.DocumentManager.MdiActiveDocument;var db=doc.Database;
 Action<string,Action> check=(name,action)=>{try{action();report.Add(new{name,pass=true});}catch(System.Exception ex){report.Add(new{name,pass=false,error=ex.ToString()});}};
 check("real drawing owner text cache",()=>{using(var tr=db.TransactionManager.StartTransaction()){var owner=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForRead);CadTextExtractor.BuildOwnerTextCache(tr,owner,new System.Collections.Generic.HashSet<string>{"A3"});}});
 check("real drawing field recognition",()=>{var jobs=TitleBlockScanner.Scan(doc,TitleBlockLibraryStore.Load());report.Add(new{jobs});if(jobs.Count==0||jobs.Any(j=>j.Title.Contains("未识别")||j.DrawingNumber.Contains("未识别")))throw new System.Exception("Some frame fields are missing");});
 check("edit reference lookup",()=>{var method=typeof(BatchPlotCommands).GetMethod("TryFindEditableReference",BindingFlags.NonPublic|BindingFlags.Static);if(!(bool)method.Invoke(null,new object[]{db,"A3",null}))throw new System.Exception("No editable reference");});
 File.WriteAllText(Path.Combine(Path.GetDirectoryName(typeof(FieldProbe).Assembly.Location),"field-check.json"),JsonConvert.SerializeObject(report,Formatting.Indented));
 }
}


