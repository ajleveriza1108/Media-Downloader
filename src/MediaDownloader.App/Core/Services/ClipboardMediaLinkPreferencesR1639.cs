using System;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace MediaDownloader.Core.Services;

public static class ClipboardMediaLinkPreferencesR1639
{
    private sealed class ClipboardPreferenceState
    {
        public bool Enabled { get; set; }
    }

    private static string StatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MediaDock",
        "clipboard-monitor.json");

    public static bool IsEnabled()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return false;
            }

            var state = JsonSerializer.Deserialize<ClipboardPreferenceState>(File.ReadAllText(StatePath));
            return state?.Enabled == true;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        var path = StatePath;
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        var json = JsonSerializer.Serialize(
            new ClipboardPreferenceState { Enabled = enabled },
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    public static bool LooksLikeMediaUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not "http" and not "https")
        {
            return false;
        }

        var host = uri.Host.Trim().ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        string[] supportedHosts =
        [
            "youtube.com", "youtu.be", "music.youtube.com",
            "facebook.com", "fb.watch", "instagram.com",
            "tiktok.com", "twitch.tv", "vimeo.com",
            "dailymotion.com", "soundcloud.com", "reddit.com",
            "x.com", "twitter.com", "rumble.com"
        ];

        return supportedHosts.Any(supported =>
            string.Equals(host, supported, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + supported, StringComparison.OrdinalIgnoreCase));
    }

    public static void RunSelfTestR1639()
    {
        if (!LooksLikeMediaUrl("https://www.youtube.com/watch?v=fixture") ||
            !LooksLikeMediaUrl("https://youtu.be/fixture") ||
            !LooksLikeMediaUrl("https://www.tiktok.com/@fixture/video/123") ||
            LooksLikeMediaUrl("not-a-url") ||
            LooksLikeMediaUrl("https://example.com/article"))
        {
            throw new InvalidOperationException("R1.6.39 clipboard media-link detection contract failed.");
        }
    }
}
