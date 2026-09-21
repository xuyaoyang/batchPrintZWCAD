using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ZwcadBatchPlot;

public sealed partial class BatchPlotForm
{
    private void ChooseLibraryStamp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ImageStampLibraryDialog(picking: true);
            if (CadDialog.ShowModal(dialog, this) != true) return;
            _stampPath.Text = dialog.SelectedImagePath;
            _stampEnabled.IsChecked = true;
            _settings.BlockStampImagePath = dialog.SelectedImagePath;
            _settings.BlockStampEnabled = true;
            AppSettingsStore.Save(_settings);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "印章库"); }
    }
    private bool SupportsImageStamp => new[] { "PDF", "PNG", "JPG" }.Contains(SelectedOutputFormat, StringComparer.OrdinalIgnoreCase);

    private void ChooseStamp_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择签名或印章图片（推荐透明 PNG）",
            Filter = "签章图片|*.png;*.jpg;*.jpeg;*.bmp",
            FileName = _stampPath.Text
        };
        if (picker.ShowDialog(this) != true) return;
        _stampPath.Text = picker.FileName;
        _stampEnabled.IsChecked = true;
        // 与批打选项一起记住用户的启用状态和图片位置。
        _settings.BlockStampImagePath = picker.FileName;
        _settings.BlockStampEnabled = true;
        AppSettingsStore.Save(_settings);
    }

    private bool ConfigureStamps(IEnumerable<PlotJob> jobs)
    {
        var path = SupportsImageStamp && _stampEnabled.IsChecked == true ? _stampPath.Text.Trim() : "";
        if (SupportsImageStamp && _stampEnabled.IsChecked == true && path.Length == 0)
        {
            MessageBox.Show(this, "请先选择签章图片。", "图片签章");
            return false;
        }
        try
        {
            foreach (var job in jobs)
            {
                job.StampImagePath = path;
                ImageStampService.Validate(job);
            }
            return true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "图片签章"); return false; }
    }

    private void PreviewStampedJob(PlotJob job)
    {
        if (!ConfigureStamps(new[] { job })) return;
        var original = job.OutputPath;
        var folder = CreateTemporaryPdfDirectory("StampPreview");
        try
        {
            ApplyLeaveMarginSelection(new[] { job });
            PrepareCustomPaperRegistrations(new[] { job }, AcadPlotterInstaller.PreferredPdfPlotter);
            job.OutputPath = Path.Combine(folder, "签章预览.pdf");
            var results = PdfRasterExport.PlotMany(new[] { job }, AcadPlotterInstaller.PreferredPdfPlotter,
                _styleCombo.SelectedItem?.ToString() ?? "", _currentDocument, _settings);
            if (results.Count != 1 || !results[0].Succeeded)
                throw results.FirstOrDefault()?.Error ?? new InvalidOperationException("签章预览未生成。");
            var imagePath = Path.Combine(folder, "签章预览.png");
            PdfRasterExport.ConvertPdfToImage(job.OutputPath, imagePath,
                RasterExportSettings.NormalizeDpi(_settings.RasterExportDpi), 90);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath);
            bitmap.EndInit();
            bitmap.Freeze();
            var preview = new Window
            {
                Title = "签章打印预览 — " + job.DrawingNumber,
                Width = 1000, Height = 760,
                Content = new System.Windows.Controls.Image
                {
                    Source = bitmap, Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(12)
                },
                Background = System.Windows.Media.Brushes.DimGray,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            CadDialog.ShowModal(preview, this);
        }
        catch (Exception ex) { MessageBox.Show(this, "签章预览失败：" + ex.Message, "图片签章"); }
        finally
        {
            job.OutputPath = original;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
