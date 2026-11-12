using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickDock.Models;
using QuickDock.Services;

namespace QuickDock.Views;

public partial class InstalledAppsWindow : Window
{
    private readonly List<ScanItem> _items;

    public InstalledAppsWindow(IReadOnlyList<InstalledApp> apps, int scannedCount, int excludedCount)
    {
        InitializeComponent();
        Height = Math.Min(680, SystemParameters.WorkArea.Height - 40);

        _items = apps.Select(app => new ScanItem(app)).ToList();
        foreach (var item in _items)
        {
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ScanItem.IsSelected)) UpdateSelectionCount();
            };
        }

        AppsList.ItemsSource = _items;
        var subtitle = $"共扫描到 {scannedCount} 个程序";
        if (excludedCount > 0) subtitle += $"，已排除 {excludedCount} 个已在列表中的项";
        SubtitleText.Text = subtitle;
        UpdateSelectionCount();
    }

    public IReadOnlyList<LauncherItem> SelectedApps { get; private set; } = [];

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        AppsList.ItemsSource = query.Length == 0
            ? _items
            : _items.Where(item =>
                    item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || item.LaunchPath.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        foreach (var item in AppsList.Items.OfType<ScanItem>())
        {
            item.IsSelected = true;
        }
    }

    private void OnSelectNone(object sender, RoutedEventArgs e)
    {
        foreach (var item in AppsList.Items.OfType<ScanItem>())
        {
            item.IsSelected = false;
        }
    }

    private void OnRowClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: ScanItem item })
        {
            item.IsSelected = !item.IsSelected;
            e.Handled = true;
        }
    }

    private void UpdateSelectionCount()
    {
        var count = _items.Count(item => item.IsSelected);
        SelectedCountText.Text = $"已选 {count} 项";
        AddButton.Content = count > 0 ? $"添加选中（{count}）" : "添加选中";
        AddButton.IsEnabled = count > 0;
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        SelectedApps = _items
            .Where(item => item.IsSelected)
            .Select(item =>
            {
                var launcherItem = new LauncherItem { Name = item.Name, Path = item.LaunchPath };
                launcherItem.Icon = IconService.Load(launcherItem);
                return launcherItem;
            })
            .ToList();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    public sealed class ScanItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private ImageSource? _icon;

        public ScanItem(InstalledApp app)
        {
            Name = app.Name;
            LaunchPath = app.LaunchPath;
            DisplayPath = app.DisplayPath;
        }

        public string Name { get; }
        public string LaunchPath { get; }
        public string DisplayPath { get; }

        public ImageSource? Icon => _icon ??= IconService.Load(LaunchPath, Name);

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
