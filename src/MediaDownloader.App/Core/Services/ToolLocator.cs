using System.IO;
using MediaDownloader.Core.Models;

namespace MediaDownloader.Core.Services;

public sealed class ToolLocator
{
    private readonly string[] _searchRoots;

    public ToolLocator()
    {
        var appRoot = AppContext.BaseDirectory;
        var current = Environment.CurrentDirectory;

        _searchRoots =
        [
            Path.Combine(appRoot, "Tools"),
            Path.Combine(current, "Tools"),
            appRoot,
            current
        ];
    }

    public string? Find(string fileName)
    {
        foreach (var root in _searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(root, fileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return FindOnPath(fileName);
    }

    public IReadOnlyList<ToolHealth> GetStaticHealth()
    {
        return
        [
            Make("yt-dlp", "yt-dlp.exe"),
            Make("Deno", "deno.exe"),
            Make("FFmpeg", "ffmpeg.exe"),
            Make("FFprobe", "ffprobe.exe")
        ];
    }

    private ToolHealth Make(string name, string fileName)
    {
        var path = Find(fileName);
        return path is null
            ? new ToolHealth(name, string.Empty, false, string.Empty, $"{fileName} was not found.")
            : new ToolHealth(name, path, true, string.Empty, "Found");
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(segment.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }
}
