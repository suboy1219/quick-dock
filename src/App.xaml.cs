using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using QuickDock.Models;
using QuickDock.Services;
using QuickDock.Views;
using WinForms = System.Windows.Forms;

namespace QuickDock;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\QuickDock.SingleInstance";
    private const string ShowSettingsEventName = @"Local\QuickDock.ShowSettings";

    private Mutex? _mutex;
    private EventWaitHandle? _showSettingsEvent;
    private RegisteredWaitHandle? _settingsWait;
    private SettingsService? _settingsService;
    private AppSettings? _settings;
    private MainWindow? _launcher;
    private SettingsWindow? _settingsWindow;
    private MouseHotkeyHook? _hook;
    private WinForms.NotifyIcon? _trayIcon;
    private WinForms.ToolStripMenuItem? _showLauncherMenuItem;
    private WinForms.ToolStripMenuItem? _startupMenuItem;
    private Icon? _trayIconImage;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            try
            {
                EventWaitHandle.OpenExisting(ShowSettingsEventName).Set();
            }
            catch
            {
                // 已有实例没在监听时忽略
            }

            Shutdown();
            return;
        }

        _showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
        _settingsWait = ThreadPool.RegisterWaitForSingleObject(
            _showSettingsEvent, (_, _) => Dispatcher.BeginInvoke(new Action(ShowSettings)),
            null, Timeout.Infinite, false);

        _settingsService = new SettingsService();
        var firstRun = !File.Exists(_settingsService.SettingsPath);
        _settings = _settingsService.Load();

        if (_settings.RunAtStartup)
        {
            TryUpdateStartup(true);
        }

        _launcher = new MainWindow(_settings, _settingsService);

        try
        {
            _hook = new MouseHotkeyHook(_settings.Hotkey);
            _hook.Triggered += () => Dispatcher.BeginInvoke(new Action(ShowLauncher));
            _hook.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"全局快捷键注册失败：{ex.Message}", "QuickDock",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        CreateTrayIcon();
        if (firstRun)
        {
            _trayIcon?.ShowBalloonTip(5000, "QuickDock 已启动",
                $"{_settings.Hotkey.DisplayText} 呼出启动器，双击托盘图标可打开设置。",
                WinForms.ToolTipIcon.Info);
        }

        if (e.Args.Any(arg => arg.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
        {
            Dispatcher.BeginInvoke(new Action(ShowSettings));
        }
        else if (e.Args.Any(arg => arg.Equals("--show", StringComparison.OrdinalIgnoreCase)))
        {
            Dispatcher.BeginInvoke(new Action(ShowLauncher));
        }
    }

    public void ShowLauncher()
    {
        _launcher?.ShowNearCursor();
    }

    public void ShowSettings()
    {
        if (_settings is null || _settingsService is null) return;

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _launcher?.Hide();
        _settingsWindow = new SettingsWindow(_settings, _settingsService, StartupManager.IsEnabled());
        _settingsWindow.Closed += (_, _) =>
        {
            var result = _settingsWindow?.Result;
            _settingsWindow = null;
            if (result is not null) ApplySettings(result);
        };
        _settingsWindow.Topmost = true;
        _settingsWindow.Show();
        var settingsHandle = new WindowInteropHelper(_settingsWindow).Handle;
        WindowActivation.ForceForeground(settingsHandle);
        _settingsWindow.Activate();
        var topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        topmostTimer.Tick += (_, _) =>
        {
            topmostTimer.Stop();
            if (_settingsWindow is not null && WindowActivation.IsForeground(settingsHandle))
            {
                _settingsWindow.Topmost = false;
            }
        };
        topmostTimer.Start();
    }

    private void ApplySettings(AppSettings result)
    {
        if (_settingsService is null) return;

        _settings = result;
        _settingsService.Save(result);
        _hook?.UpdateConfig(result.Hotkey);
        _launcher?.UpdateSettings(result);

        if (_showLauncherMenuItem is not null)
        {
            _showLauncherMenuItem.ShortcutKeyDisplayString = result.Hotkey.DisplayText;
        }

        try
        {
            StartupManager.SetEnabled(result.RunAtStartup);
            if (_startupMenuItem is not null) _startupMenuItem.Checked = result.RunAtStartup;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"更新开机启动设置失败：{ex.Message}", "QuickDock",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CreateTrayIcon()
    {
        _trayIconImage = TrayIconFactory.Create();
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "QuickDock - 鼠标旁的快速启动器",
            Visible = true
        };

        var menu = new WinForms.ContextMenuStrip
        {
            BackColor = Color.FromArgb(30, 30, 36),
            ForeColor = Color.White,
            Renderer = new WinForms.ToolStripProfessionalRenderer(new DarkMenuColorTable()),
            ShowImageMargin = false
        };

        _showLauncherMenuItem = new WinForms.ToolStripMenuItem("显示启动器");
        _showLauncherMenuItem.ShortcutKeyDisplayString = _settings?.Hotkey.DisplayText;
        _showLauncherMenuItem.Click += (_, _) => ShowLauncher();

        var settingsItem = new WinForms.ToolStripMenuItem("设置…");
        settingsItem.Click += (_, _) => ShowSettings();

        _startupMenuItem = new WinForms.ToolStripMenuItem("开机自动启动")
        {
            CheckOnClick = true,
            Checked = StartupManager.IsEnabled(),
            ForeColor = Color.White
        };
        _startupMenuItem.Click += (_, _) => ToggleStartup(_startupMenuItem.Checked);

        var exitItem = new WinForms.ToolStripMenuItem("退出 QuickDock");
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(_showLauncherMenuItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(HelpMenu.Create());
        menu.Items.Add(exitItem);

        foreach (WinForms.ToolStripItem item in menu.Items)
        {
            item.ForeColor = Color.White;
        }

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    private void ToggleStartup(bool enabled)
    {
        try
        {
            StartupManager.SetEnabled(enabled);
            if (_settings is not null)
            {
                _settings.RunAtStartup = enabled;
                _settingsService?.Save(_settings);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"更新开机启动设置失败：{ex.Message}", "QuickDock",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            if (_startupMenuItem is not null) _startupMenuItem.Checked = !enabled;
        }
    }

    private void TryUpdateStartup(bool enabled)
    {
        try
        {
            StartupManager.SetEnabled(enabled);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"同步开机启动失败: {ex}");
        }
    }

    private void ExitApplication()
    {
        if (_settings is not null) _settingsService?.Save(_settings);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        _settingsWait?.Unregister(null);
        _showSettingsEvent?.Dispose();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _trayIconImage?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickDock");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 日志写入失败时忽略
        }

        MessageBox.Show($"QuickDock 遇到一个错误：{e.Exception.Message}", "QuickDock",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private sealed class DarkMenuColorTable : WinForms.ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 36);
        public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 36);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 36);
        public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 36);
        public override Color MenuBorder => Color.FromArgb(58, 58, 70);
        public override Color MenuItemBorder => Color.FromArgb(91, 140, 255);
        public override Color MenuItemSelected => Color.FromArgb(45, 45, 55);
        public override Color SeparatorDark => Color.FromArgb(58, 58, 70);
        public override Color SeparatorLight => Color.FromArgb(58, 58, 70);
    }
}
