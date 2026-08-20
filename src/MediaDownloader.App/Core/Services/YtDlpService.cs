using System.Globalization;
using System.IO;
using System.Text.Json;
using MediaDownloader.Core.Models;

namespace MediaDownloader.Core.Services;

public sealed class YtDlpService
{
    private const string ProgressTemplate = "download:MDPROGRESS|%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes,progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s|%(info.playlist_index)s|%(info.playlist_count)s|%(info.title)s";

    private readonly ToolLocator _tools;
    private readonly ProcessRunner _runner;

    public YtDlpService(ToolLocator tools, ProcessRunner runner)
    {
        _tools = tools;
        _runner = runner;
    }

    public async Task<MediaAnalysisResult> AnalyzeAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var target = NormalizeTarget(url);
        var ytDlp = RequireTool("yt-dlp.exe");
        var args = BuildCommonArguments();

        args.Add("--dump-single-json");

        if (target.ForceSingleVideo)
        {
            // A YouTube watch/short/live URL always means the selected video,
            // even when YouTube adds list=RD..., list=PL..., index=, or start_radio=1.
            args.Add("--no-playlist");
        }
        else
        {
            // Explicit playlist URLs and non-YouTube collection URLs may return a
            // playlist object. Flat mode keeps discovery efficient.
            args.Add("--flat-playlist");
            args.Add("--yes-playlist");
            args.Add("--ignore-errors");
        }

        args.Add(target.Url);

        var result = await _runner.RunAsync(ytDlp, args, cancellationToken).ConfigureAwait(false);
        var diagnostics = BuildDiagnostics(result);

        if (!result.Success)
        {
            throw new MediaEngineException("Analysis failed.", diagnostics);
        }

        var json = ExtractJsonObject(result.StandardOutput);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new MediaEngineException("Could not read metadata from yt-dlp.", diagnostics);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!target.ForceSingleVideo && LooksLikePlaylist(root))
        {
            var playlist = ParsePlaylist(root, target.Url);
            if (playlist.Entries.Count == 0)
            {
                throw new MediaEngineException("The playlist did not contain any accessible entries.", diagnostics);
            }

            return new MediaAnalysisResult(null, playlist, diagnostics);
        }

        var formats = ParseFormats(root);
        if (formats.Count == 0)
        {
            throw new MediaEngineException("No downloadable non-DRM media formats were returned.", diagnostics);
        }

        var media = new MediaInfo(
            Id: GetString(root, "id"),
            Title: GetString(root, "title", "Untitled media"),
            WebpageUrl: target.ForceSingleVideo ? target.Url : GetString(root, "webpage_url", target.Url),
            Extractor: GetString(root, "extractor_key", GetString(root, "extractor", "Unknown")),
            Uploader: GetString(root, "uploader", GetString(root, "channel", string.Empty)),
            DurationSeconds: GetDouble(root, "duration"),
            ThumbnailUrl: GetString(root, "thumbnail", string.Empty),
            Formats: formats);

        return new MediaAnalysisResult(media, null, diagnostics);
    }

    public async Task<string> DownloadAsync(
        MediaInfo media,
        QualityChoice? qualityChoice,
        AudioChoice? audioChoice,
        OutputFormatKind outputFormat,
        int mp3BitrateKbps,
        string outputDirectory,
        CancellationToken cancellationToken = default,
        Action<string>? onProgress = null)
    {
        Directory.CreateDirectory(outputDirectory);

        var ytDlp = RequireTool("yt-dlp.exe");
        var args = BuildCommonArguments();

        args.Add("--no-playlist");
        AddProgressArguments(args);
        args.Add("--paths");
        args.Add(outputDirectory);
        args.Add("--output");
        args.Add("%(title).180B [%(id)s].%(ext)s");

        AddFormatArguments(args, outputFormat, qualityChoice, audioChoice, mp3BitrateKbps);
        args.Add(NormalizeTarget(media.WebpageUrl).Url);

        return await RunDownloadAsync(
            ytDlp,
            args,
            outputDirectory,
            "Download failed.",
            cancellationToken,
            onProgress,
            requireOutput: true);
    }

    public async Task<string> DownloadPlaylistAsync(
        PlaylistInfo playlist,
        OutputFormatKind outputFormat,
        int mp3BitrateKbps,
        string outputDirectory,
        CancellationToken cancellationToken = default,
        Action<string>? onProgress = null)
    {
        Directory.CreateDirectory(outputDirectory);

        var ytDlp = RequireTool("yt-dlp.exe");
        var args = BuildCommonArguments();

        args.Add("--yes-playlist");
        args.Add("--ignore-errors");
        AddProgressArguments(args);
        args.Add("--paths");
        args.Add(outputDirectory);
        args.Add("--output");
        args.Add("%(playlist_title)s/%(playlist_index)03d - %(title).180B [%(id)s].%(ext)s");

        AddFormatArguments(
            args,
            outputFormat,
            new QualityChoice(QualityChoiceKind.Best, null, "Auto - Best available per video"),
            null,
            mp3BitrateKbps);

        args.Add(playlist.WebpageUrl);

        return await RunDownloadAsync(
            ytDlp,
            args,
            outputDirectory,
            "Playlist download failed.",
            cancellationToken,
            onProgress,
            requireOutput: true);
    }

    public static IReadOnlyList<QualityChoice> BuildQualityChoices(MediaInfo media)
    {
        var choices = new List<QualityChoice>
        {
            new(QualityChoiceKind.Best, null, "Auto - Best available")
        };

        var representatives = media.Formats
            .Where(format => format.HasVideo && !format.IsDrm && format.Height is > 0)
            .GroupBy(format => new
            {
                Height = format.Height!.Value,
                Fps = NormalizeFps(format.Fps)
            })
            .Select(group => group
                .OrderBy(format => format.HasAudio ? 1 : 0)
                .ThenByDescending(format => format.VideoBitrate ?? 0)
                .ThenByDescending(format => format.FileSize ?? 0)
                .First())
            .OrderByDescending(format => format.Height ?? 0)
            .ThenByDescending(format => NormalizeFps(format.Fps));

        foreach (var format in representatives)
        {
            var height = format.Height!.Value;
            var fps = NormalizeFps(format.Fps);
            var fpsText = fps > 0 ? $"{fps}" : string.Empty;
            var dimensions = format.Width is > 0
                ? $" · {format.Width.Value}×{height}"
                : string.Empty;
            var label = $"{height}p{fpsText}{dimensions}";

            choices.Add(new QualityChoice(
                QualityChoiceKind.ExactHeight,
                height,
                label,
                fps > 0 ? fps : null,
                string.IsNullOrWhiteSpace(format.FormatId) ? null : format.FormatId,
                format.HasAudio));
        }

        if (media.Formats.Any(format => format.HasAudio && !format.HasVideo && !format.IsDrm))
        {
            choices.Add(new QualityChoice(QualityChoiceKind.AudioOnly, null, "Audio only"));
        }

        return choices;
    }

    public static IReadOnlyList<QualityChoice> BuildPlaylistQualityChoices() =>
    [
        new QualityChoice(QualityChoiceKind.Best, null, "Best available per video")
    ];

    public static IReadOnlyList<AudioChoice> BuildAudioChoices(MediaInfo media)
    {
        var choices = new List<AudioChoice>
        {
            new("Best available audio")
        };

        var representatives = media.Formats
            .Where(format => format.HasAudio && !format.HasVideo && !format.IsDrm && !string.IsNullOrWhiteSpace(format.FormatId))
            .GroupBy(format => new
            {
                Codec = NormalizeCodecLabel(format.AudioCodec),
                Bitrate = format.AudioBitrate is > 0 ? (int)Math.Round(format.AudioBitrate.Value) : 0,
                Extension = format.Extension.ToUpperInvariant()
            })
            .Select(group => group.OrderByDescending(format => format.AudioBitrate ?? 0).First())
            .OrderByDescending(format => format.AudioBitrate ?? 0)
            .Take(8);

        foreach (var format in representatives)
        {
            var codec = NormalizeCodecLabel(format.AudioCodec);
            var bitrate = format.AudioBitrate is > 0
                ? $" · {Math.Round(format.AudioBitrate.Value):0} kbps"
                : string.Empty;
            var extension = string.IsNullOrWhiteSpace(format.Extension)
                ? string.Empty
                : $" · {format.Extension.ToUpperInvariant()}";
            choices.Add(new AudioChoice($"{codec}{bitrate}{extension}", format.FormatId));
        }

        return choices;
    }

    public static QualityChoice? SelectPreferredDefaultQuality(IEnumerable<QualityChoice> choices)
    {
        var all = choices.ToArray();
        var exact = all
            .Where(choice => choice.Kind == QualityChoiceKind.ExactHeight && choice.Height is > 0)
            .ToArray();

        var preferred = exact
            .Where(choice => choice.Height == 1080)
            .OrderByDescending(choice => choice.Fps ?? 0)
            .FirstOrDefault();
        if (preferred is not null)
        {
            return preferred;
        }

        preferred = exact
            .Where(choice => choice.Height == 720)
            .OrderByDescending(choice => choice.Fps ?? 0)
            .FirstOrDefault();
        if (preferred is not null)
        {
            return preferred;
        }

        preferred = exact
            .OrderByDescending(choice => choice.Height ?? 0)
            .ThenByDescending(choice => choice.Fps ?? 0)
            .FirstOrDefault();

        return preferred
            ?? all.FirstOrDefault(choice => choice.Kind == QualityChoiceKind.Best)
            ?? all.FirstOrDefault();
    }

    public static AudioChoice? SelectPreferredDefaultAudio(IEnumerable<AudioChoice> choices)
    {
        var all = choices.ToArray();
        return all.FirstOrDefault(choice => !string.IsNullOrWhiteSpace(choice.FormatId))
            ?? all.FirstOrDefault();
    }

    public static string NormalizeUserUrl(string url) => NormalizeTarget(url).Url;

    private async Task<string> RunDownloadAsync(
        string ytDlp,
        IReadOnlyList<string> args,
        string outputDirectory,
        string failureMessage,
        CancellationToken cancellationToken,
        Action<string>? onProgress,
        bool requireOutput)
    {
        string? finalPath = null;
        const int maxDiagnosticLines = 4000;
        var log = new List<string>();

        void AppendLog(string line)
        {
            if (log.Count >= maxDiagnosticLines)
            {
                log.RemoveAt(0);
            }
            log.Add(line);
        }

        void HandleOutput(string line)
        {
            AppendLog(line);
            if (line.StartsWith("MDOUTPUT|", StringComparison.Ordinal))
            {
                finalPath = line["MDOUTPUT|".Length..].Trim();
            }

            onProgress?.Invoke(line);
        }

        void HandleError(string line)
        {
            AppendLog(line);
            onProgress?.Invoke(line);
        }

        var result = await _runner.RunAsync(ytDlp, args, cancellationToken, HandleOutput, HandleError).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new MediaEngineException(failureMessage, string.Join(Environment.NewLine, log));
        }

        if (string.IsNullOrWhiteSpace(finalPath))
        {
            finalPath = FindNewestOutput(outputDirectory);
        }

        if (requireOutput && string.IsNullOrWhiteSpace(finalPath))
        {
            throw new MediaEngineException(
                "The operation completed without producing a media file.",
                string.Join(Environment.NewLine, log));
        }

        return finalPath ?? string.Empty;
    }

    private static void AddProgressArguments(List<string> args)
    {
        args.Add("--newline");
        args.Add("--progress");
        args.Add("--progress-delta");
        args.Add("0.35");
        args.Add("--progress-template");
        args.Add(ProgressTemplate);
        args.Add("--print");
        args.Add("after_move:MDOUTPUT|%(filepath)s");
    }

    private static void AddFormatArguments(
        List<string> args,
        OutputFormatKind outputFormat,
        QualityChoice? qualityChoice,
        AudioChoice? audioChoice,
        int mp3BitrateKbps)
    {
        if (outputFormat == OutputFormatKind.Mp3)
        {
            args.Add("--format");
            args.Add(string.IsNullOrWhiteSpace(audioChoice?.FormatId) ? "bestaudio/best" : audioChoice!.FormatId!);
            args.Add("--extract-audio");
            args.Add("--audio-format");
            args.Add("mp3");
            args.Add("--audio-quality");
            args.Add(string.Format(CultureInfo.InvariantCulture, "{0}K", mp3BitrateKbps));
            return;
        }

        args.Add("--format");
        args.Add(BuildFormatSelector(qualityChoice, audioChoice));
        args.Add("--merge-output-format");
        args.Add(outputFormat == OutputFormatKind.Mp4 ? "mp4" : "mkv");
    }

    private List<string> BuildCommonArguments()
    {
        var args = new List<string>
        {
            "--ignore-config",
            "--no-colors",
            "--encoding", "utf-8"
        };

        var ffmpeg = _tools.Find("ffmpeg.exe");
        if (ffmpeg is not null)
        {
            args.Add("--ffmpeg-location");
            args.Add(Path.GetDirectoryName(ffmpeg)!);
        }

        var deno = _tools.Find("deno.exe");
        if (deno is not null)
        {
            args.Add("--js-runtimes");
            args.Add($"deno:{deno}");
        }

        return args;
    }

    private static string BuildFormatSelector(QualityChoice? choice, AudioChoice? audioChoice)
    {
        var audioSelector = string.IsNullOrWhiteSpace(audioChoice?.FormatId) ? "bestaudio" : audioChoice!.FormatId!;

        if (choice is null)
        {
            return $"bestvideo+{audioSelector}/best";
        }

        return choice.Kind switch
        {
            QualityChoiceKind.Best => $"bestvideo+{audioSelector}/best",
            QualityChoiceKind.AudioOnly => string.IsNullOrWhiteSpace(audioChoice?.FormatId) ? "bestaudio/best" : audioChoice!.FormatId!,
            QualityChoiceKind.ExactHeight when !string.IsNullOrWhiteSpace(choice.FormatId) && choice.FormatHasAudio =>
                choice.FormatId!,
            QualityChoiceKind.ExactHeight when !string.IsNullOrWhiteSpace(choice.FormatId) =>
                $"{choice.FormatId}+{audioSelector}",
            QualityChoiceKind.ExactHeight when choice.Height is > 0 =>
                $"bestvideo[height={choice.Height.Value}]+{audioSelector}",
            _ => throw new InvalidOperationException("Invalid quality selection.")
        };
    }

    private static MediaTarget NormalizeTarget(string url)
    {
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return new MediaTarget(trimmed, false, false);
        }

        var host = uri.Host.ToLowerInvariant();
        var isYoutube = host == "youtu.be" || host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal);
        if (!isYoutube)
        {
            return new MediaTarget(trimmed, false, false);
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        var query = ParseQuery(uri.Query);

        if (host == "youtu.be")
        {
            var id = path.Trim('/');
            if (!string.IsNullOrWhiteSpace(id))
            {
                return new MediaTarget(
                    $"https://www.youtube.com/watch?v={Uri.EscapeDataString(id)}",
                    true,
                    false);
            }
        }

        if (string.Equals(path, "/watch", StringComparison.OrdinalIgnoreCase) &&
            query.TryGetValue("v", out var videoId) &&
            !string.IsNullOrWhiteSpace(videoId))
        {
            // This intentionally strips list/index/start_radio parameters. A watch
            // URL means the selected video, including YouTube Mix/radio URLs.
            return new MediaTarget(
                $"https://www.youtube.com/watch?v={Uri.EscapeDataString(videoId)}",
                true,
                false);
        }

        if (string.Equals(path, "/playlist", StringComparison.OrdinalIgnoreCase) &&
            query.TryGetValue("list", out var playlistId) &&
            !string.IsNullOrWhiteSpace(playlistId))
        {
            return new MediaTarget(
                $"https://www.youtube.com/playlist?list={Uri.EscapeDataString(playlistId)}",
                false,
                true);
        }

        if (path.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/live/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
        {
            return new MediaTarget(trimmed, true, false);
        }

        return new MediaTarget(trimmed, false, false);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
            var value = pair.Length > 1
                ? Uri.UnescapeDataString(pair[1].Replace('+', ' '))
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string NormalizeCodecLabel(string codec)
    {
        if (string.IsNullOrWhiteSpace(codec) || string.Equals(codec, "none", StringComparison.OrdinalIgnoreCase))
        {
            return "Audio";
        }

        var lower = codec.ToLowerInvariant();
        if (lower.Contains("opus", StringComparison.Ordinal)) return "Opus";
        if (lower.Contains("mp4a", StringComparison.Ordinal) || lower.Contains("aac", StringComparison.Ordinal)) return "AAC";
        if (lower.Contains("mp3", StringComparison.Ordinal)) return "MP3";
        return codec;
    }

    private static int NormalizeFps(double? fps)
    {
        if (fps is null || fps <= 0)
        {
            return 0;
        }

        return (int)Math.Round(fps.Value, MidpointRounding.AwayFromZero);
    }

    private static bool LooksLikePlaylist(JsonElement root)
    {
        var type = GetString(root, "_type");
        if (string.Equals(type, "playlist", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "multi_video", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array;
    }

    private static PlaylistInfo ParsePlaylist(JsonElement root, string fallbackUrl)
    {
        var entries = new List<PlaylistEntryInfo>();
        if (root.TryGetProperty("entries", out var entryArray) && entryArray.ValueKind == JsonValueKind.Array)
        {
            var index = 1;
            foreach (var entry in entryArray.EnumerateArray())
            {
                if (entry.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    index++;
                    continue;
                }

                var id = GetString(entry, "id");
                var entryTitle = GetString(entry, "title", string.IsNullOrWhiteSpace(id) ? $"Item {index}" : id);
                var webpageUrl = GetString(entry, "webpage_url", GetString(entry, "url", string.Empty));
                var thumbnail = GetString(entry, "thumbnail", string.Empty);
                entries.Add(new PlaylistEntryInfo(index, id, entryTitle, webpageUrl, thumbnail));
                index++;
            }
        }

        var rootThumbnail = GetString(root, "thumbnail", string.Empty);
        if (string.IsNullOrWhiteSpace(rootThumbnail))
        {
            rootThumbnail = entries.Select(entry => entry.ThumbnailUrl).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        var playlistTitle = GetString(root, "title", GetString(root, "playlist_title", "Untitled playlist"));
        var uploader = GetString(
            root,
            "uploader",
            GetString(root, "playlist_uploader", GetString(root, "channel", string.Empty)));

        return new PlaylistInfo(
            Id: GetString(root, "id", GetString(root, "playlist_id", string.Empty)),
            Title: playlistTitle,
            WebpageUrl: GetString(root, "webpage_url", fallbackUrl),
            Extractor: GetString(root, "extractor_key", GetString(root, "extractor", "Unknown")),
            Uploader: uploader,
            ThumbnailUrl: rootThumbnail,
            Entries: entries);
    }

    private string RequireTool(string fileName) =>
        _tools.Find(fileName) ?? throw new InvalidOperationException($"Required tool {fileName} was not found in the Tools folder or PATH.");

    private static string BuildDiagnostics(ProcessResult result) =>
        $"Exit code: {result.ExitCode}{Environment.NewLine}{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}";

    private static string? ExtractJsonObject(string stdout)
    {
        var trimmed = stdout.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        foreach (var line in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            var candidate = line.Trim();
            if (candidate.StartsWith('{') && candidate.EndsWith('}'))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IReadOnlyList<MediaFormat> ParseFormats(JsonElement root)
    {
        var list = new List<MediaFormat>();
        if (!root.TryGetProperty("formats", out var formats) || formats.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var format in formats.EnumerateArray())
        {
            if (format.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            var formatId = GetString(format, "format_id");
            var vcodec = GetString(format, "vcodec", "none");
            var acodec = GetString(format, "acodec", "none");
            var hasVideo = !string.Equals(vcodec, "none", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(vcodec);
            var hasAudio = !string.Equals(acodec, "none", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(acodec);
            var drm = GetBoolean(format, "has_drm");

            if ((!hasVideo && !hasAudio) || drm)
            {
                continue;
            }

            list.Add(new MediaFormat(
                FormatId: formatId,
                Width: GetInt(format, "width"),
                Height: GetInt(format, "height"),
                Fps: GetDouble(format, "fps"),
                Extension: GetString(format, "ext"),
                VideoCodec: vcodec,
                AudioCodec: acodec,
                Protocol: GetString(format, "protocol"),
                FileSize: GetLong(format, "filesize") ?? GetLong(format, "filesize_approx"),
                VideoBitrate: GetDouble(format, "vbr"),
                AudioBitrate: GetDouble(format, "abr"),
                HasVideo: hasVideo,
                HasAudio: hasAudio,
                IsDrm: drm));
        }

        return list;
    }

    private static string GetString(JsonElement element, string name, string fallback = "")
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static long? GetLong(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static double? GetDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static bool GetBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        return value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var result) && result;
    }

    private static string? FindNewestOutput(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory)
                .Where(path => !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private sealed record MediaTarget(string Url, bool ForceSingleVideo, bool ForcePlaylist);
}

public sealed class MediaEngineException : Exception
{
    public MediaEngineException(string message, string diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics;
    }

    public string Diagnostics { get; }
}
