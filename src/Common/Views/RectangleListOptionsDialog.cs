using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ZwcadBatchPlot;

public sealed class RectangleListOptionsDialog : Window
{
    public string Prefix { get; private set; } = "";
    public string Suffix { get; private set; } = "";
    public int Start { get; private set; }
    public int Digits { get; private set; }
    public bool HorizontalFirst { get; private set; }
    public double SmallFramePercent { get; private set; }

    public RectangleListOptionsDialog(AppSettings settings, string stem, bool filterOnly = false)
    {
        Title = filterOnly ? "小图框过滤" : "文件命名与排序";
        Width = 470; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(18) }; Content = root;
        TextBox Field(string label,string text) {
            root.Children.Add(new TextBlock { Text=label, Margin = new Thickness(0,6,0,3) });
            var box = new TextBox { Text=text, Padding=new Thickness(4),MinHeight=25 }; root.Children.Add(box); return box;
        }
        var prefix = Field("前缀（留空时使用各自 DWG 文件名）",settings.RectangleNamePrefix);
        var suffix = Field("后缀",settings.RectangleNameSuffix);
        var start = Field("起始编号",settings.RectangleSequenceStart.ToString());
        var digits = Field("编号位数（1–10）",settings.RectangleSequenceDigits.ToString());
        var sort = new ComboBox { ItemsSource = new[] { "从左到右，再从上到下", "从上到下，再从左到右" }, SelectedIndex = settings.SortOrderHorizontalFirst ? 0 : 1, Margin = new Thickness(0,10,0,8) }; root.Children.Add(sort);
        var sample = new TextBlock { Margin = new Thickness(0,5,0,10), TextWrapping = TextWrapping.Wrap }; root.Children.Add(sample);
        Action preview = () => { if(int.TryParse(start.Text,out var s) && int.TryParse(digits.Text,out var d)) sample.Text = "示例：" + RectangleListOptions.FileName(stem,prefix.Text,suffix.Text,s,0,d,".pdf") + "\n          " + RectangleListOptions.FileName(stem,prefix.Text,suffix.Text,s,1,d,".pdf"); };
        foreach (var box in new[] { prefix,suffix,start,digits }) box.TextChanged += (_,_) => preview(); preview();
        if(filterOnly) foreach(UIElement child in root.Children) child.Visibility = Visibility.Collapsed;
        var percent = Field("过滤面积小于本文件、本空间最大图框面积的百分比（0–100）",settings.RectangleSmallFramePercent.ToString(CultureInfo.InvariantCulture));
        if(!filterOnly) { percent.Visibility = Visibility.Collapsed; ((UIElement)root.Children[root.Children.Count-2]).Visibility = Visibility.Collapsed; }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,14,0,0) }; root.Children.Add(buttons);
        var ok = new Button {Content="确定",Width=80,Padding=new Thickness(5)}; var cancel = new Button {Content="取消",Width=80,Padding=new Thickness(5),Margin=new Thickness(8,0,0,0),IsCancel=true}; buttons.Children.Add(ok); buttons.Children.Add(cancel);
        ok.Click += (_,_) => {
            if(!int.TryParse(start.Text,out var s)||s<0||!int.TryParse(digits.Text,out var d)||d<1||d>10||!double.TryParse(percent.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var p)||double.IsNaN(p)||p<0||p>100) { MessageBox.Show(this,"请检查编号、位数及过滤百分比。",Title); return; }
            if((prefix.Text+suffix.Text).IndexOfAny(System.IO.Path.GetInvalidFileNameChars())>=0) { MessageBox.Show(this,"前后缀不能包含文件名非法字符。",Title);return; }
            Prefix=prefix.Text;Suffix=suffix.Text;Start=s;Digits=d;HorizontalFirst=sort.SelectedIndex==0;SmallFramePercent=p;DialogResult=true;
        };
    }
}
