using System.Diagnostics;
using QuickDock.Models;

namespace QuickDock.Services;

public static class AppLauncher
{
    public static bool Launch(LauncherItem item, out string? error)
    {
        error = null;
        try
        {
            var rawPath = item.Path.Trim();
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                error = "启动路径为空";
                return false;
            }

            if (rawPath.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                var shellInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{rawPath}\"",
                    UseShellExecute = true
                };
                Process.Start(shellInfo);
                item.LaunchCount++;
                item.LastUsedAt = DateTime.Now;
                return true;
            }

            var path = Environment.ExpandEnvironmentVariables(rawPath);
            var info = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };

            if (!string.IsNullOrWhiteSpace(item.Arguments))
            {
                info.Arguments = item.Arguments;
            }

            var workdir = string.IsNullOrWhiteSpace(item.WorkingDirectory)
                ? (File.Exists(path) ? Path.GetDirectoryName(path) : null)
                : Environment.ExpandEnvironmentVariables(item.WorkingDirectory);
            if (!string.IsNullOrWhiteSpace(workdir) && Directory.Exists(workdir))
            {
                info.WorkingDirectory = workdir;
            }

            Process.Start(info);
            item.LaunchCount++;
            item.LastUsedAt = DateTime.Now;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
