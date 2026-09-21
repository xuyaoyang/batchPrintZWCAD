using System;
using System.Linq;
using ZwcadBatchPlot;
class Program
{
 public static int Main()
 {
  int failed=0;
  Action<bool,string> check=(ok,name)=>{Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)failed++;};
  var options=PaperSizeDetector.CreateRectangleBatchOptions(1,false,3,null);
  foreach(var input in new[]{new{w=29700d,h=21000d,first="A4",next="A3",scale=21000d/297},new{w=42000d,h=29700d,first="A3",next="A2",scale=29700d/420}})
  {
   foreach(var portrait in new[]{false,true})
   {
    var choices=PaperSizeDetector.DetectRectangleBatchCandidates(portrait?input.h:input.w,portrait?input.w:input.h,options);
    Console.WriteLine(string.Join("; ",choices.Select(PaperSizeDetector.FormatOption)));
    check(choices[0].PaperName==input.first && Math.Abs(choices[0].ScaleValue-100)<.001,input.first+" unchanged default");
    var next=choices.FirstOrDefault(p=>p.PaperName==input.next);
    check(next!=null,input.first+" -> "+input.next+(portrait?" portrait":" landscape"));
    check(next!=null && Math.Abs(next.ScaleValue-input.scale)<.001,"converted scale");
    check(choices.Where(p=>!p.IsLong).Select(p=>p.PaperName).Distinct().Count()==5,"all A0-A4");
    check(choices.All(p=>portrait?p.PaperWidthMm<=p.PaperHeightMm:p.PaperWidthMm>=p.PaperHeightMm),"orientation");
   }
  }
  var layout=PaperSizeDetector.DetectRectangleBatchCandidates(297,210,PaperSizeDetector.CreateRectangleBatchOptions(1,true,3,null));
  check(layout[0].PaperName=="A4"&&Math.Abs(layout[0].ScaleValue-1)<.001,"layout remains 1:1");
  check(layout.Any(p=>p.PaperName=="A3"),"layout adjacent size");
  var longPaper=PaperSizeDetector.DetectRectangleBatchCandidates(84100d*10/8,59400,options);
  check(longPaper[0].IsLong&&longPaper[0].PaperName.StartsWith("A1+"),"extended frame remains extended");
  check(!longPaper.Any(p=>!p.IsLong),"no standard-sheet distortion of extended frames");
  check(PaperSizeDetector.DetectRectangleBatchCandidates(10000,10000,options).Count==0,"square remains rejected");
  Console.WriteLine("Failures: "+failed);return failed==0?0:1;
 }
}
