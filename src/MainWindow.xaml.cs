using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using QuickDock.Models;
using QuickDock.Services;

namespace QuickDock;

public partial class MainWindow : Window
{
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private readonly SettingsService _settingsService;

    public MainWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        Settings = settings;
        _settingsService = settingsService;
        ReloadItems();
    }

    public AppSettings Settings { get; private set; }

    public void UpdateSettings(AppSettings settings)
    {
        Settings = settings;
        ReloadItems();
    }

    public void ReloadItems()
    {
        HotkeyHint.Text = Settings.Hotkey.DisplayText;

        var items = Settings.SortByRecent
            ? Settings.Apps
                .OrderByDescending(a => a.LastUsedAt ?? DateTime.MinValue)
                .ThenByDescending(a => a.LaunchCount)
                .ToList()
            : Settings.Apps.ToList();

        foreach (var item in items)
        {
            item.Icon ??= IconService.Load(item);
        }

        AppsList.ItemsSource = null;
        AppsList.ItemsSource = items;
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountHint.Text = items.Count == 0 ? string.Empty : $"{items.Count} 项";
    }

    public void ShowNearCursor()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        ReloadItems();
        Opacity = 0;
        Show();
        UpdateLayout();

        var handle = new WindowInteropHelper(this).Handle;
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var work = screen.WorkingArea;
        var dpiScale = GetDpiForWindow(handle) / 96.0;
        var width = (int)Math.Ceiling(ActualWidth * dpiScale);
        var height = (int)Math.Ceiling(ActualHeight * dpiScale);
        var cursor = System.Windows.Forms.Cursor.Position;

        var x = cursor.X + 16;
        var y = cursor.Y + 18;
        if (x + width > work.Right) x = cursor.X - width - 16;
        if (y + height > work.Bottom) y = cursor.Y - height - 18;
        x = Math.Max(work.Left + 8, Math.Min(x, work.Right - width - 8));
        y = Math.Max(work.Top + 8, Math.Min(y, work.Bottom - height - 8));

        SetWindowPos(handle, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);

        WindowActivation.ForceForeground(handle);
        Activate();
        if (AppsList.Items.Count > 0)
        {
            AppsList.SelectedIndex = 0;
        }

        AppsList.Focus();
        Opacity = 1;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (IsVisible) Hide();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && AppsList.SelectedItem is LauncherItem selected)
        {
            Launch(selected);
            e.Handled = true;
            return;
        }

        var index = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            _ => -1
        };

        if (index >= 0 && index < AppsList.Items.Count)
        {
            Launch((LauncherItem)AppsList.Items[index]);
            e.Handled = true;
        }
    }

    private void OnItemMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: LauncherItem item })
        {
            Launch(item);
            e.Handled = true;
        }
    }

    private void Launch(LauncherItem item)
    {
        Hide();
        if (AppLauncher.Launch(item, out var error))
        {
            _settingsService.Save(Settings);
            return;
        }

        MessageBox.Show($"无法启动“{item.Name}”：{error}", "QuickDock",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
