using System;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ZwcadBatchPlot;
class Program {
 static int failures;
 static void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
 static int Main(string[] args){var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);var input=Path.Combine(root,"page.pdf");using(var pdf=new PdfDocument()){pdf.AddPage();pdf.Save(input);}
 var output=Path.Combine(root,"版本V2.pdf");File.WriteAllText(output,"EXISTING VERSION");
 var inputs=new[]{new PdfMergeInput(input,"Sheet","A3",420,297)};
 var plan=PdfDocumentService.PlanMerges(inputs,output,false).Single();Check(plan.OutputPath!=output,"default merge plan preserves existing version");
 bool rejected=false;try{PdfDocumentService.Merge(inputs,output,true);}catch(IOException){rejected=true;}Check(rejected&&File.ReadAllText(output)=="EXISTING VERSION","merge refuses late collision without deleting old file");
 var fresh=Path.Combine(root,"自定义名称V3.pdf");var freshPlan=PdfDocumentService.PlanMerges(inputs,fresh,false).Single();Check(freshPlan.OutputPath==fresh,"custom filename retained");PdfDocumentService.Merge(inputs,freshPlan.OutputPath,true);using(var pdf=PdfReader.Open(fresh,PdfDocumentOpenMode.Import)){Check(pdf.PageCount==1,"custom named PDF contains page");}
 PdfDocumentService.Merge(inputs,plan.OutputPath,true);Check(File.Exists(plan.OutputPath)&&File.ReadAllText(output)=="EXISTING VERSION","numbered merged PDF preserves old version");
 Check(!Directory.GetFiles(root,"*.tmp.pdf").Any(),"temporary output cleaned");return failures==0?0:1;
 }
}
