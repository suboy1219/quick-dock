using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickDock.Models;

namespace QuickDock.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SettingsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickDock");

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            if (!File.Exists(SettingsPath))
            {
                var defaults = AppSettings.CreateDefault();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null) throw new InvalidDataException("配置文件内容为空");
            settings.Hotkey ??= new HotkeyConfig();
            settings.Apps ??= new List<LauncherItem>();
            return settings;
        }
        catch (Exception ex)
        {
            TryBackupBrokenFile();
            var fallback = AppSettings.CreateDefault();
            fallback.Hotkey = new HotkeyConfig();
            TrySave(fallback);
            System.Diagnostics.Debug.WriteLine($"读取配置失败: {ex}");
            return fallback;
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            TrySave(settings);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"保存配置失败：{ex.Message}", "QuickDock",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private void TrySave(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = SettingsPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsPath, true);
    }

    private void TryBackupBrokenFile()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                File.Copy(SettingsPath, SettingsPath + ".broken", true);
            }
        }
        catch
        {
            // 备份失败不应阻止程序启动
        }
    }
}
