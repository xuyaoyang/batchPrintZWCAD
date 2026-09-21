using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ZwcadBatchPlot;

public static class RectangleListOptions
{
    public static string FileName(string sourceStem, string prefix, string suffix, int start, int index, int digits, string extension)
    {
        var stem = string.IsNullOrEmpty(prefix) ? sourceStem : prefix;
        var number = checked((long)Math.Max(0,start) + index);
        return stem + number.ToString("D" + Math.Max(1,Math.Min(10,digits)),CultureInfo.InvariantCulture) + suffix + extension;
    }
    public static HashSet<PlotJob> KeepFrames(IEnumerable<PlotJob> jobs, double percent)
    {
        var keep = new HashSet<PlotJob>();
        percent = double.IsNaN(percent) ? 0 : Math.Max(0,Math.Min(100,percent));
        foreach (var group in jobs.GroupBy(j => (j.SourceFile ?? "").ToUpperInvariant() + "\n" + (j.SpaceName ?? "").ToUpperInvariant()))
        {
            var list = group.ToList();
            var max = list.Max(Area);
            foreach(var job in list) if (percent <= 0 || Area(job) >= max * percent / 100d) keep.Add(job);
        }
        return keep;
    }
    private static double Area(PlotJob job) => job.UsesUserCoordinateSystem
        ? Math.Abs((job.UcsMaxX-job.UcsMinX)*(job.UcsMaxY-job.UcsMinY))
        : Math.Abs((job.MaxX-job.MinX)*(job.MaxY-job.MinY));
}
