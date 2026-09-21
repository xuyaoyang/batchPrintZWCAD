using System;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace ZwcadBatchPlot;

/// <summary>只修改本次生成的 PDF；栅格输出在此之后转图，合并也在此之后进行。</summary>
public static class ImageStampService
{
    public static void Validate(PlotJob job)
    {
        if (string.IsNullOrWhiteSpace(job.StampImagePath)) return;
        if (string.IsNullOrWhiteSpace(job.BlockHandle) || job.StampWorldCorners?.Length != 8)
            throw new InvalidOperationException($"图框“{job.BlockName}”尚未设置签章区域，请在图框库中编辑并框选后重新扫描。");
        if (!File.Exists(job.StampImagePath)) throw new FileNotFoundException("签章图片不存在，请重新选择。", job.StampImagePath);
        using var image = XImage.FromFile(job.StampImagePath);
        if (image.PixelWidth <= 0 || image.PixelHeight <= 0) throw new InvalidOperationException("签章图片无效。");
    }

    /// <summary>输入为显示坐标中的签章四角 BL/BR/TR/TL。图像等比居中，不拉伸。</summary>
    public static void Apply(string pdfPath, string imagePath, double[] corners,
        double minX, double minY, double maxX, double maxY, double scaleMm,
        double leftMm = 0, double bottomMm = 0, double rightMm = 0, double topMm = 0)
    {
        var temp = pdfPath + ".stamp-" + Guid.NewGuid().ToString("N") + ".pdf";
        try
        {
            using (var pdf = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify))
            using (var image = XImage.FromFile(imagePath))
            {
                foreach (var page in pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>())
                {
                    NormalizePageRotation(page);
                    const double pointsPerMm = 72d / 25.4d;
                    var width = page.Width.Point;
                    var height = page.Height.Point;
                    var dw = maxX - minX;
                    var dh = maxY - minY;
                    if (dw <= 0 || dh <= 0) throw new InvalidOperationException("签章打印窗口无效。");
                    var scale = scaleMm > 0 ? scaleMm * pointsPerMm
                        : Math.Min((width - (leftMm + rightMm) * pointsPerMm) / dw,
                            (height - (topMm + bottomMm) * pointsPerMm) / dh);
                    var ox = (width + (leftMm - rightMm) * pointsPerMm - dw * scale) / 2;
                    var oy = (height + (topMm - bottomMm) * pointsPerMm - dh * scale) / 2;
                    XPoint Map(int i) => new(ox + (corners[i * 2] - minX) * scale,
                        oy + (maxY - corners[i * 2 + 1]) * scale);
                    var tl = Map(3); var tr = Map(2); var bl = Map(0);
                    var ux = tr.X - tl.X; var uy = tr.Y - tl.Y;
                    var vx = bl.X - tl.X; var vy = bl.Y - tl.Y;
                    var rw = Math.Sqrt(ux * ux + uy * uy);
                    var rh = Math.Sqrt(vx * vx + vy * vy);
                    if (rw < 1e-6 || rh < 1e-6) throw new InvalidOperationException("签章区域没有面积。");
                    var fit = Math.Min(rw / image.PixelWidth, rh / image.PixelHeight);
                    var iw = image.PixelWidth * fit; var ih = image.PixelHeight * fit;
                    using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    graphics.MultiplyTransform(new XMatrix(ux / rw, uy / rw, vx / rh, vy / rh, tl.X, tl.Y));
                    graphics.DrawImage(image, (rw - iw) / 2, (rh - ih) / 2, iw, ih);
                }
                pdf.Save(temp);
            }
            File.Copy(temp, pdfPath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    // CAD 有时用横向 MediaBox + Rotate=270 表达竖版。先将原有矢量内容
    // 转至可见页面坐标，再叠图；只改输出 PDF，且保留文字与矢量。
    private static void NormalizePageRotation(PdfSharp.Pdf.PdfPage page)
    {
        var rotation = ((page.Rotate % 360) + 360) % 360;
        if (rotation == 0) return;
        var box = page.MediaBox;
        var w = box.Width; var h = box.Height;
        string matrix;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string N(double v) => v.ToString("0.########", inv);
        if (rotation == 90) matrix = $"0 -1 1 0 {N(-box.Y1)} {N(w + box.X1)}";
        else if (rotation == 180) matrix = $"-1 0 0 -1 {N(w + box.X1)} {N(h + box.Y1)}";
        else if (rotation == 270) matrix = $"0 1 -1 0 {N(h + box.Y1)} {N(-box.X1)}";
        else throw new InvalidOperationException("PDF 页面旋转角度不受支持。");
        page.Contents.PrependContent().CreateStream(System.Text.Encoding.ASCII.GetBytes("q\n" + matrix + " cm\n"));
        page.Contents.AppendContent().CreateStream(System.Text.Encoding.ASCII.GetBytes("\nQ\n"));
        page.Rotate = 0;
        page.Orientation = PdfSharp.PageOrientation.Portrait;
        var normalized = new PdfSharp.Pdf.PdfRectangle(new XRect(0, 0, rotation == 180 ? w : h, rotation == 180 ? h : w));
        page.MediaBox = normalized;
        page.CropBox = normalized;
    }
}
