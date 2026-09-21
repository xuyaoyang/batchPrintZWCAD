using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZwcadBatchPlot;

public sealed class ImageStampLibraryDialog : Window
{
    private readonly ObservableCollection<ImageStampEntry> _entries;
    private readonly DataGrid _grid;
    private readonly Image _preview = new() { Stretch = Stretch.Uniform, Margin = new Thickness(12) };
    private readonly bool _picking;
    private readonly string _path;
    public string SelectedImagePath { get; private set; } = "";

    public ImageStampLibraryDialog(bool picking = false, string? libraryPath = null)
    {
        _picking = picking;
        _path = libraryPath ?? Path.Combine(TitleBlockLibraryStore.DefaultDirectory, "ImageStampLibrary.json");
        _entries = new ObservableCollection<ImageStampEntry>(ImageStampLibrary.Load(_path));
        Title = picking ? "选择印章 · LA批量打印" : "印章库管理 · LA批量打印";
        Width = 840; Height = 490; MinWidth = 700; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 12;
        Background = SystemColors.ControlBrush;
        var root = new DockPanel { Margin = new Thickness(12) }; Content = root;
        var hint = new TextBlock { Text = "录入透明 PNG 或签名图片；打印时放入图框的签章区域。仅用于图框型 PDF / PNG / JPG。", Margin = new Thickness(0,0,0,10), TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(hint, Dock.Top); root.Children.Add(hint);
        var buttons = new WrapPanel { Margin = new Thickness(0,10,0,0) };
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        AddButton(buttons, "录入新印章", AddImage);
        AddButton(buttons, "移出印章库", () => { if (_grid!.SelectedItem is ImageStampEntry item) _entries.Remove(item); });
        AddButton(buttons, "导入印章库", Import);
        AddButton(buttons, "导出印章库", Export);
        AddButton(buttons, picking ? "使用选中印章" : "保存", Save);
        AddButton(buttons, "取消", () => { DialogResult = false; });
        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(225) }); root.Children.Add(body);
        _grid = new DataGrid { ItemsSource = _entries, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Single, HeadersVisibility = DataGridHeadersVisibility.Column };
        _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "启用", Binding = new Binding("Enabled"), Width = 55 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "印章名称", Binding = new Binding("Name"), MinWidth = 180, Width = new DataGridLength(1,DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "图片尺寸（像素）", Binding = new Binding("PixelSize"), IsReadOnly = true, Width = 135 });
        _grid.SelectionChanged += (_, _) => Preview(); body.Children.Add(_grid);
        var border = new Border { Background = Brushes.White, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Margin = new Thickness(10,0,0,0), Child = _preview };
        Grid.SetColumn(border,1); body.Children.Add(border);
        if (_entries.Count > 0) _grid.SelectedIndex = 0;
    }
    private void AddButton(Panel panel, string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(9,5,9,5), Margin = new Thickness(0,0,7,0), MinHeight = 28 };
        button.Click += (_, _) => { try { Commit(); action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "印章库"); } };
        panel.Children.Add(button);
    }
    private void Commit() { _grid.CommitEdit(DataGridEditingUnit.Cell,true); _grid.CommitEdit(DataGridEditingUnit.Row,true); }
    private void Preview()
    {
        _preview.Source = null;
        if (_grid.SelectedItem is not ImageStampEntry item) return;
        using var stream = new MemoryStream(Convert.FromBase64String(item.PngBase64));
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); _preview.Source = bitmap;
    }
    private void AddImage()
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "录入印章图片", Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp", Multiselect = true };
        if (picker.ShowDialog(this) != true) return;
        foreach (var file in picker.FileNames) { var item = ImageStampLibrary.FromImage(file); _entries.Add(item); _grid.SelectedItem = item; }
    }
    private void Import()
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "导入 LA 图片印章库", Filter = "LA 图片印章库|*.lastamps.json" };
        if (picker.ShowDialog(this) == true) ImageStampLibrary.Merge(_entries, ImageStampLibrary.Load(picker.FileName));
    }
    private void Export()
    {
        var picker = new Microsoft.Win32.SaveFileDialog { Title = "导出含图片的印章库", Filter = "LA 图片印章库|*.lastamps.json", FileName = "印章库.lastamps.json" };
        if (picker.ShowDialog(this) == true) ImageStampLibrary.Save(picker.FileName, _entries);
    }
    private void Save()
    {
        if (_picking) {
            if (_grid.SelectedItem is not ImageStampEntry item) throw new InvalidOperationException("请选择要使用的印章。");
            SelectedImagePath = ImageStampLibrary.Materialize(item, Path.Combine(Path.GetDirectoryName(_path)!, "StampImages"));
        }
        ImageStampLibrary.Save(_path,_entries); DialogResult = true;
    }
}
