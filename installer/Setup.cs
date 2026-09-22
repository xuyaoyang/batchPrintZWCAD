using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class CadTarget
{
    internal string Label, Key, Platform, Dll, App;
    public override string ToString() { return Label; }
}

internal static class Setup
{
    internal static string PlatformForAcad(string release)
    {
        switch (release) {
            case "R20.0": case "R20.1": case "R21.0": case "R22.0":
            case "R23.0": case "R23.1": case "R24.0": case "R24.1": case "R24.2": case "R24.3": return "acad48";
            case "R25.0": case "R25.1": case "R25.2": return "acad8";
            default: return null;
        }
    }
    internal static string AcadName(string release)
    {
        string[] releases = { "R20.0", "R20.1", "R21.0", "R22.0", "R23.0", "R23.1", "R24.0", "R24.1", "R24.2", "R24.3", "R25.0", "R25.1", "R25.2" };
        int index = Array.IndexOf(releases, release);
        return index < 0 ? release : (2015 + index).ToString();
    }
    internal static List<CadTarget> Detect(RegistryKey root)
    {
        var result = new List<CadTarget>();
        const string acad = @"Software\Autodesk\AutoCAD";
        using (var parent = root.OpenSubKey(acad)) {
            if (parent != null) foreach (var release in parent.GetSubKeyNames()) {
                string platform = PlatformForAcad(release);
                if (platform == null) continue;
                using (var version = parent.OpenSubKey(release)) foreach (var product in version.GetSubKeyNames()) {
                    if (!product.StartsWith("ACAD-", StringComparison.OrdinalIgnoreCase) || !product.Contains(":")) continue;
                    result.Add(new CadTarget { Label = "AutoCAD " + AcadName(release) + " / " + product + (platform == "acad8" ? "（尚未实机验证）" : ""),
                        Key = acad + "\\" + release + "\\" + product, Platform = platform,
                        Dll = platform == "acad8" ? "AcadBatchPlot.Core.dll" : "AcadBatchPlot.dll", App = "AcadBatchPlot" });
                }
            }
        }
        foreach (var family in new[] { "ZWCAD", "ZWCADM" }) {
            string key = @"Software\ZWSOFT\" + family + @"\2026";
            using (var version = root.OpenSubKey(key)) {
                if (version == null) continue;
                foreach (var language in version.GetSubKeyNames()) {
                    if (!language.Contains("-")) continue;
                    result.Add(new CadTarget { Label = (family == "ZWCADM" ? "中望机械 CAD" : "中望 CAD") + " 2026 / " + language,
                        Key = key + "\\" + language, Platform = "zw", Dll = "BatchPlotter.dll", App = "ZwcadBatchPlot" });
                }
            }
        }
        return result;
    }
    internal static void Register(RegistryKey root, CadTarget target, string folder)
    {
        string dll = Path.Combine(folder, target.Platform, target.Dll);
        if (!File.Exists(dll)) throw new FileNotFoundException("安装文件不完整", dll);
        using (var key = root.CreateSubKey(target.Key + @"\Applications\" + target.App)) {
            key.SetValue("DESCRIPTION", "LA批量打印（图片签章版）");
            key.SetValue("LOADER", dll);
            key.SetValue("LOADCTRLS", 2, RegistryValueKind.DWord);
            key.SetValue("MANAGED", 1, RegistryValueKind.DWord);
        }
    }
    internal static void Extract(string folder)
    {
        Directory.CreateDirectory(folder);
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read)) {
            foreach (var entry in archive.Entries) {
                string path = Path.GetFullPath(Path.Combine(folder, entry.FullName));
                if (!path.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("无效安装路径");
                if (entry.Name.Length == 0) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                entry.ExtractToFile(path, false);
            }
        }
    }
    internal static string Install(List<CadTarget> targets)
    {
        // A fresh version directory also permits upgrades while CAD has an old DLL loaded.
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LA BatchPlot", "20260922-R6-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Extract(folder);
        var done = new StringBuilder();
        foreach (var target in targets) {
            try { Register(Registry.CurrentUser, target, folder); done.AppendLine("已安装：" + target.Label); }
            catch (Exception ex) { done.AppendLine("安装失败：" + target.Label + " — " + ex.Message); }
        }
        return done + "\r\n目录：" + folder + "\r\n\r\n下次启动 CAD 自动加载。在“LA批量打印”菜单使用。\r\n当前 CAD 会话保持不变。";
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try {
            if (args.Length > 0 && args[0] == "--self-test") return SelfTest(args[1]);
            var targets = Detect(Registry.CurrentUser);
            if (args.Length > 0 && args[0] == "--list") {
                var lines = new List<string>();
                foreach (var target in targets) lines.Add(target.Label + " | " + target.Key + " | " + target.Platform);
                File.WriteAllLines(args[1], lines.ToArray()); return 0;
            }
            Application.EnableVisualStyles();
            var form = new Form { Text = "LA批量打印 · 图片签章版安装", Width = 640, Height = 420, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            var info = new Label { Left = 22, Top = 18, Width = 580, Height = 76, Text = "选择要安装的 CAD（仅当前 Windows 用户，无需管理员权限）。\r\n包含图片签章、PDF/PNG/JPG 输出及相邻纸张切换。\r\n已实测：中望机械2026、AutoCAD2023；其他版本以实际验证为准。" };
            var list = new CheckedListBox { Left = 22, Top = 100, Width = 580, Height = 155, CheckOnClick = true };
            foreach (var target in targets) list.Items.Add(target, true);
            var note = new Label { Left = 22, Top = 269, Width = 580, Height = 48, Text = "未列出的 CAD 请先启动一次，再重新运行安装包。\r\n支持中望2026、AutoCAD2015–2027；不需要手动 NETLOAD。" };
            var button = new Button { Text = "安装", Left = 490, Top = 328, Width = 110, Height = 32, Enabled = targets.Count > 0 };
            button.Click += delegate {
                var selected = new List<CadTarget>();
                foreach (CadTarget target in list.CheckedItems) selected.Add(target);
                if (selected.Count == 0) { MessageBox.Show(form, "请选择至少一个 CAD。"); return; }
                button.Enabled = false;
                try { MessageBox.Show(form, Install(selected), "安装结果"); form.Close(); }
                catch (Exception ex) { MessageBox.Show(form, ex.Message, "安装失败"); button.Enabled = true; }
            };
            form.Controls.AddRange(new Control[] { info, list, note, button });
            Application.Run(form); return 0;
        } catch (Exception ex) {
            if (args.Length > 1) File.WriteAllText(args[1], ex.ToString());
            else MessageBox.Show(ex.Message, "安装失败");
            return 1;
        }
    }
    private static int SelfTest(string report)
    {
        string name = @"Software\LA-BatchPlot-Installer-Test-" + Guid.NewGuid().ToString("N");
        string folder = Path.Combine(Path.GetTempPath(), "LA-Installer-Test-" + Guid.NewGuid().ToString("N"));
        try {
            Extract(folder);
            using (var root = Registry.CurrentUser.CreateSubKey(name)) {
                using (root.CreateSubKey(@"Software\Autodesk\AutoCAD\R24.2\ACAD-6101:804")) { }
                using (root.CreateSubKey(@"Software\Autodesk\AutoCAD\R25.0\ACAD-8101:804")) { }
                using (root.CreateSubKey(@"Software\Autodesk\AutoCAD\R19.1\ACAD-OLD:804")) { }
                using (root.CreateSubKey(@"Software\ZWSOFT\ZWCADM\2026\zh-CN")) { }
                var targets = Detect(root);
                if (targets.Count != 3) throw new Exception("Detection failed");
                foreach (var target in targets) {
                    Register(root, target, folder);
                    Register(root, target, folder);
                    using (var key = root.OpenSubKey(target.Key + @"\Applications\" + target.App)) {
                        if ((int)key.GetValue("LOADCTRLS") != 2 || (int)key.GetValue("MANAGED") != 1 || !File.Exists((string)key.GetValue("LOADER"))) throw new Exception("Registry failed");
                    }
                    if (!File.Exists(Path.Combine(folder, target.Platform, "pdfium.dll")) || !Directory.Exists(Path.Combine(folder, target.Platform, "Plotters"))) throw new Exception("Dependencies missing");
                }
                if (PlatformForAcad("R25.3") != null || PlatformForAcad("R24.3") != "acad48" || PlatformForAcad("R25.2") != "acad8") throw new Exception("Version routing failed");
            }
            File.WriteAllText(report, "PASS: embedded payload extraction; 3 platform dependencies; CAD detection and unsupported exclusion; .NET runtime routing; HKCU registration; repeat registration.\r\nNo real CAD registry keys changed.");
            return 0;
        } finally {
            Registry.CurrentUser.DeleteSubKeyTree(name, false);
            if (Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) Directory.Delete(folder, true);
        }
    }
}
