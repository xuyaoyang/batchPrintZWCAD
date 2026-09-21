using System;
using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using ZwcadBatchPlot;
class CoreProgram
{
 static int Main(string[] args)
 {
  if(Program.Main()!=0)return 1;
  var dir=args[0];Directory.CreateDirectory(dir);
  foreach(var rotation in new[]{0,90,180,270})
  {
   var file=Path.Combine(dir,"core-"+rotation+".pdf");
   using(var pdf=new PdfDocument())
   {
    var page=pdf.AddPage();page.Width=XUnit.FromMillimeter(420);page.Height=XUnit.FromMillimeter(297);
    using(var g=XGraphics.FromPdfPage(page))g.DrawRectangle(XPens.Black,20,20,100,40);
    page.Rotate=rotation;pdf.Save(file);
   }
   var portrait=rotation==90||rotation==270;
   ImageStampService.Apply(file,args[1],new double[]{20,20,100,20,100,60,20,60},0,0,portrait?297:420,portrait?420:297,1);
  }
  Console.WriteLine("PASS Core DLL paper checks and PDFsharp 6.2.4 PNG stamping for 4 page rotations; runner=.NET9, no AutoCAD host");
  return 0;
 }
}
