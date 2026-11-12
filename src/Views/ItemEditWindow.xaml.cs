using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using QuickDock.Models;
using QuickDock.Services;

namespace QuickDock.Views;

public partial class ItemEditWindow : Window
{
    private readonly bool _isNew;

    public ItemEditWindow(LauncherItem? item)
    {
        InitializeComponent();
        _isNew = item is null;
        HeaderText.Text = _isNew ? "添加应用" : "编辑应用";

        if (item is not null)
        {
            NameBox.Text = item.Name;
            PathBox.Text = item.Path;
            ArgsBox.Text = item.Arguments;
            WorkDirBox.Text = item.WorkingDirectory;
        }

        Loaded += (_, _) => NameBox.Focus();
    }

    public LauncherItem? Result { get; private set; }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnPathChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isNew && string.IsNullOrWhiteSpace(NameBox.Text) && !string.IsNullOrWhiteSpace(PathBox.Text))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(PathBox.Text.Trim());
            if (!string.IsNullOrWhiteSpace(name)) NameBox.Text = name;
        }
    }

    private void OnBrowseFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择应用",
            Filter = "程序与快捷方式|*.exe;*.lnk;*.bat;*.cmd;*.url|所有文件|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            PathBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择工作目录" };
        if (dialog.ShowDialog(this) == true)
        {
            WorkDirBox.Text = dialog.FolderName;
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var path = PathBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("请填写显示名称。", "QuickDock", MessageBoxButton.OK,
                MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("请填写启动路径。", "QuickDock", MessageBoxButton.OK,
                MessageBoxImage.Information);
            PathBox.Focus();
            return;
        }

        var item = new LauncherItem
        {
            Name = name,
            Path = path,
            Arguments = ArgsBox.Text.Trim(),
            WorkingDirectory = WorkDirBox.Text.Trim()
        };
        item.Icon = IconService.Load(item);
        Result = item;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
