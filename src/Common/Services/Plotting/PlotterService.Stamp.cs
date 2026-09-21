using System;
using System.Linq;
#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
#else
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;
#endif

namespace ZwcadBatchPlot;

public static partial class PlotterService
{
    private static void ApplyImageStamp(PlotJob job, PlotSettings settings, Document? document)
    {
        if (string.IsNullOrWhiteSpace(job.StampImagePath)) return;
        ImageStampService.Validate(job);
        var matrix = Matrix3d.Identity;
        if (!job.IsPaperSpace && document != null)
        {
            using var view = document.Editor.GetCurrentView();
            matrix = GetWorldToDisplayMatrix(view);
        }
        var cp = job.StampWorldCorners!;
        var points = Enumerable.Range(0, 4).Select(i => new Point3d(cp[i * 2], cp[i * 2 + 1], 0).TransformBy(matrix)).ToArray();
        var window = settings.PlotWindowArea;
        var margins = settings.PlotPaperMargins;
        double left = margins.MinPoint.X, bottom = margins.MinPoint.Y, right = margins.MaxPoint.X, top = margins.MaxPoint.Y;
        if (settings.PlotRotation == PlotRotation.Degrees090 || settings.PlotRotation == PlotRotation.Degrees270)
        {
            // 打印后 PDF 的页面方向跟随绘图窗口；将物理纸张边距也旋至同一方向。
            (left, bottom, right, top) = settings.PlotRotation == PlotRotation.Degrees090
                ? (bottom, right, top, left) : (top, left, bottom, right);
        }
        var custom = settings.CustomPrintScale;
        // AutoCAD 校验 PC3 后可能把纸张单位设为英寸，自定义比例也随之换算。
        // PDF 叠章接口统一接收毫米/图面单位。
        var millimetersPerUnit = custom.Numerator / custom.Denominator
            * (settings.PlotPaperUnits == PlotPaperUnit.Inches ? 25.4d : 1d);
        ImageStampService.Apply(job.OutputPath, job.StampImagePath,
            points.SelectMany(p => new[] { p.X, p.Y }).ToArray(),
            window.MinPoint.X, window.MinPoint.Y, window.MaxPoint.X, window.MaxPoint.Y,
            settings.UseStandardScale ? 0 : millimetersPerUnit,
            left, bottom, right, top);
    }
}
