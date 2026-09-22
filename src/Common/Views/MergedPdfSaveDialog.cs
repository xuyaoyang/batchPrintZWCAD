using System.IO;

namespace ZwcadBatchPlot;

internal static class MergedPdfSaveDialog
{
    public static string? Choose(string suggestedPath)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "设置合并 PDF 名称（同名自动加序号，保留已有文件）",
            Filter = "PDF 文件 (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = Path.GetFileName(suggestedPath),
            InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(suggestedPath)),
            OverwritePrompt = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
