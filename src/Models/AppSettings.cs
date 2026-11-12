namespace QuickDock.Models;

public sealed class AppSettings
{
    public HotkeyConfig Hotkey { get; set; } = new();
    public List<LauncherItem> Apps { get; set; } = new();
    public bool RunAtStartup { get; set; }
    public bool SortByRecent { get; set; }

    public static AppSettings CreateDefault()
    {
        var system32 = Environment.SystemDirectory;
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        return new AppSettings
        {
            Apps =
            [
                new LauncherItem
                {
                    Name = "资源管理器",
                    Path = Path.Combine(windows, "explorer.exe")
                },
                new LauncherItem
                {
                    Name = "记事本",
                    Path = Path.Combine(system32, "notepad.exe")
                },
                new LauncherItem
                {
                    Name = "计算器",
                    Path = Path.Combine(system32, "calc.exe")
                },
                new LauncherItem
                {
                    Name = "画图",
                    Path = Path.Combine(system32, "mspaint.exe")
                },
                new LauncherItem
                {
                    Name = "系统设置",
                    Path = "ms-settings:"
                }
            ]
        };
    }
}
