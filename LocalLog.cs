using System.IO;

namespace PerformanceOverlay;

internal static class LocalLog
{
    private static readonly object Sync = new();
    private const long MaxTotalBytes = 1024L * 1024 * 1024;
    private const long MaxFileBytes = 10L * 1024 * 1024;
    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PerformanceOverlay", "Logs");

    public static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                var path = Path.Combine(LogDirectory, $"performance-overlay-{DateTime.UtcNow:yyyyMMdd}.log");
                var line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
                var bytes = System.Text.Encoding.UTF8.GetByteCount(line);
                if (File.Exists(path) && new FileInfo(path).Length + bytes > MaxFileBytes)
                    path = Path.Combine(LogDirectory, $"performance-overlay-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.log");
                File.AppendAllText(path, line);
                TrimOldestLogs();
            }
        }
        catch { }
    }

    private static void TrimOldestLogs()
    {
        var files = new DirectoryInfo(LogDirectory).GetFiles("*.log")
            .OrderBy(file => file.LastWriteTimeUtc).ToArray();
        long total = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            if (total <= MaxTotalBytes) break;
            try { total -= file.Length; file.Delete(); }
            catch { }
        }
    }
}
