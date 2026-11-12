using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using QuickDock.Models;
using QuickDock.Services;

namespace QuickDock.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ObservableCollection<LauncherItem> _apps;

    public SettingsWindow(AppSettings settings, SettingsService settingsService, bool startupEnabled)
    {
        InitializeComponent();
        Height = Math.Min(860, SystemParameters.WorkArea.Height - 40);
        _settingsService = settingsService;
        _apps = new ObservableCollection<LauncherItem>(settings.Apps.Select(CloneItem));
        AppsList.ItemsSource = _apps;

        AltBox.IsChecked = settings.Hotkey.Alt;
        CtrlBox.IsChecked = settings.Hotkey.Ctrl;
        ShiftBox.IsChecked = settings.Hotkey.Shift;
        WinBox.IsChecked = settings.Hotkey.Win;

        var triggerRadio = settings.Hotkey.Trigger switch
        {
            MouseTrigger.Right => RightRadio,
            MouseTrigger.XButton1 => X1Radio,
            MouseTrigger.XButton2 => X2Radio,
            _ => MiddleRadio
        };
        triggerRadio.IsChecked = true;

        StartupBox.IsChecked = startupEnabled;
        RecentBox.IsChecked = settings.SortByRecent;
        ConfigPathText.Text = $"配置：{settingsService.SettingsPath}";

        AppsList.MouseDoubleClick += (_, _) => EditSelected();
    }

    public AppSettings? Result { get; private set; }

    private static LauncherItem CloneItem(LauncherItem source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Path = source.Path,
        Arguments = source.Arguments,
        WorkingDirectory = source.WorkingDirectory,
        LaunchCount = source.LaunchCount,
        LastUsedAt = source.LastUsedAt,
        Icon = source.Icon ?? IconService.Load(source)
    };

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnFilesDropped(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        foreach (var path in paths)
        {
            AddPath(path, select: false);
        }

        if (_apps.Count > 0) AppsList.SelectedIndex = _apps.Count - 1;
    }

    private void OnAddProgram(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要添加的应用",
            Multiselect = true,
            Filter = "程序与快捷方式|*.exe;*.lnk;*.bat;*.cmd;*.url|所有文件|*.*"
        };

        if (dialog.ShowDialog(this) != true) return;
        foreach (var file in dialog.FileNames)
        {
            AddPath(file, select: false);
        }

        AppsList.SelectedIndex = _apps.Count - 1;
    }

    private void OnScanInstalled(object sender, RoutedEventArgs e)
    {
        List<InstalledApp> scanned;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            scanned = InstalledAppScanner.Scan();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"扫描已安装程序失败：{ex.Message}", "QuickDock",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        if (scanned.Count == 0)
        {
            MessageBox.Show("没有扫描到可用的已安装程序。", "QuickDock",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var existingPaths = new HashSet<string>(
            _apps.Select(app => app.Path), StringComparer.OrdinalIgnoreCase);
        var existingNames = new HashSet<string>(
            _apps.Select(app => app.Name), StringComparer.CurrentCultureIgnoreCase);
        var candidates = scanned
            .Where(app => !existingPaths.Contains(app.LaunchPath)
                          && !existingNames.Contains(app.Name))
            .ToList();
        var excludedCount = scanned.Count - candidates.Count;

        if (candidates.Count == 0)
        {
            MessageBox.Show("扫描到的程序都已经在列表中了。", "QuickDock",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new InstalledAppsWindow(candidates, scanned.Count, excludedCount)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true || dialog.SelectedApps.Count == 0) return;

        foreach (var item in dialog.SelectedApps)
        {
            _apps.Add(item);
        }

        AppsList.SelectedIndex = _apps.Count - 1;
        AppsList.ScrollIntoView(_apps[^1]);
    }

    private void AddPath(string path, bool select = true)
    {
        var item = new LauncherItem
        {
            Name = GetDisplayName(path),
            Path = path
        };
        item.Icon = IconService.Load(item);
        _apps.Add(item);
        if (select) AppsList.SelectedItem = item;
        AppsList.ScrollIntoView(item);
    }

    private static string GetDisplayName(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                var description = info.FileDescription?.Trim();
                if (!string.IsNullOrWhiteSpace(description)) return description;
                var product = info.ProductName?.Trim();
                if (!string.IsNullOrWhiteSpace(product)) return product;
            }
        }
        catch
        {
            // 读不到版本信息就退回文件名
        }

        var name = Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/'));
        return string.IsNullOrWhiteSpace(name) ? "新应用" : name;
    }

    private void OnAddCustom(object sender, RoutedEventArgs e)
    {
        var dialog = new ItemEditWindow(null) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        var item = dialog.Result;
        item.Icon = IconService.Load(item);
        _apps.Add(item);
        AppsList.SelectedItem = item;
        AppsList.ScrollIntoView(item);
    }

    private void OnEdit(object sender, RoutedEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (AppsList.SelectedItem is not LauncherItem item) return;

        var dialog = new ItemEditWindow(item) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        item.Name = dialog.Result.Name;
        item.Path = dialog.Result.Path;
        item.Arguments = dialog.Result.Arguments;
        item.WorkingDirectory = dialog.Result.WorkingDirectory;
        item.Icon = IconService.Load(item);
        AppsList.Items.Refresh();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (AppsList.SelectedItem is not LauncherItem item) return;
        var index = _apps.IndexOf(item);
        _apps.Remove(item);
        if (_apps.Count > 0)
        {
            AppsList.SelectedIndex = Math.Min(index, _apps.Count - 1);
        }
    }

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        var index = AppsList.SelectedIndex;
        if (index <= 0) return;
        _apps.Move(index, index - 1);
        AppsList.SelectedIndex = index - 1;
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        var index = AppsList.SelectedIndex;
        if (index < 0 || index >= _apps.Count - 1) return;
        _apps.Move(index, index + 1);
        AppsList.SelectedIndex = index + 1;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (AltBox.IsChecked != true && CtrlBox.IsChecked != true
            && ShiftBox.IsChecked != true && WinBox.IsChecked != true)
        {
            MessageBox.Show("请至少选择一个修饰键，否则普通的鼠标点击也会触发启动器。",
                "QuickDock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var invalid = _apps.FirstOrDefault(a => string.IsNullOrWhiteSpace(a.Path));
        if (invalid is not null)
        {
            MessageBox.Show($"“{invalid.Name}”没有设置启动路径。", "QuickDock",
                MessageBoxButton.OK, MessageBoxImage.Information);
            AppsList.SelectedItem = invalid;
            return;
        }

        var trigger = MiddleRadio.IsChecked == true ? MouseTrigger.Middle
            : RightRadio.IsChecked == true ? MouseTrigger.Right
            : X1Radio.IsChecked == true ? MouseTrigger.XButton1
            : MouseTrigger.XButton2;

        Result = new AppSettings
        {
            Hotkey = new HotkeyConfig
            {
                Alt = AltBox.IsChecked == true,
                Ctrl = CtrlBox.IsChecked == true,
                Shift = ShiftBox.IsChecked == true,
                Win = WinBox.IsChecked == true,
                Trigger = trigger
            },
            Apps = _apps.ToList(),
            RunAtStartup = StartupBox.IsChecked == true,
            SortByRecent = RecentBox.IsChecked == true
        };

        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnOpenConfigDir(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_settingsService.SettingsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_settingsService.SettingsDirectory}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开目录：{ex.Message}", "QuickDock",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
