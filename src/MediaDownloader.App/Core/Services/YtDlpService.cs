using System.Globalization;
using System.IO;
using System.Text.Json;
using MediaDownloader.Core.Models;

namespace MediaDownloader.Core.Services;

public sealed class YtDlpService
{
    private const string ProgressTemplate = "download:MDPROGRESS|%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes,progress.total_bytes_estimate)s|%(progress.speed)s|%(info.playlist_index)s|%(info.playlist_count)s|%(info.title)s";

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

    public async Task<StreamMediaInfo> ResolveStreamAsync(
        string url,
        QualityChoice? qualityChoice = null,
        CancellationToken cancellationToken = default)
    {
        var target = NormalizeTarget(url);
        var ytDlp = RequireTool("yt-dlp.exe");
        var args = BuildCommonArguments();

        args.Add("--no-playlist");
        args.Add("--dump-single-json");
        args.Add("--format");
        args.Add(BuildStreamFormatSelector(qualityChoice));
        args.Add(target.Url);

        var result = await _runner.RunAsync(
            ytDlp,
            args,
            cancellationToken).ConfigureAwait(false);

        var diagnostics = BuildDiagnostics(result);
        if (!result.Success)
        {
            throw new MediaEngineException(
                "MediaDock could not resolve the selected directly playable stream.",
                diagnostics);
        }

        var json = ExtractJsonObject(result.StandardOutput);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new MediaEngineException(
                "yt-dlp returned no directly playable stream metadata.",
                diagnostics);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var directUrl = GetString(root, "url", string.Empty).Trim();
        if (!Uri.TryCreate(directUrl, UriKind.Absolute, out var directUri) ||
            directUri.Scheme is not "http" and not "https")
        {
            throw new MediaEngineException(
                "The selected format did not expose a direct HTTP media URL.",
                diagnostics);
        }

        var vcodec = GetString(root, "vcodec", "none");
        var acodec = GetString(root, "acodec", "none");
        var hasVideo =
            !string.Equals(vcodec, "none", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(vcodec);
        var hasAudio =
            !string.Equals(acodec, "none", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(acodec);

        if (!hasVideo && !hasAudio)
        {
            throw new MediaEngineException(
                "The selected format did not expose playable audio or video.",
                diagnostics);
        }

        return new StreamMediaInfo(
            Title: GetString(root, "title", "Detected media"),
            WebpageUrl: GetString(root, "webpage_url", target.Url),
            DirectUrl: directUri.AbsoluteUri,
            Extractor: GetString(root, "extractor_key", GetString(root, "extractor", "Unknown")),
            Uploader: GetString(root, "uploader", GetString(root, "channel", string.Empty)),
            DurationSeconds: GetDouble(root, "duration"),
            ThumbnailUrl: GetString(root, "thumbnail", string.Empty),
            Extension: GetString(root, "ext", string.Empty),
            Protocol: GetString(root, "protocol", string.Empty),
            HasVideo: hasVideo,
            HasAudio: hasAudio);
    }

    public static IReadOnlyList<QualityChoice> BuildStreamQualityChoices(MediaInfo media)
    {
        var choices = new List<QualityChoice>
        {
            new(QualityChoiceKind.Best, null, "Auto - Internal media detector")
        };

        var combined = media.Formats
            .Where(format =>
                format.HasVideo &&
                format.HasAudio &&
                !format.IsDrm &&
                format.Height is > 0 &&
                !string.IsNullOrWhiteSpace(format.FormatId))
            .GroupBy(format => new
            {
                Height = format.Height!.Value,
                Fps = NormalizeFps(format.Fps)
            })
            .Select(group => group
                .OrderByDescending(format =>
                    string.Equals(format.Extension, "mp4", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenByDescending(format => format.VideoBitrate ?? 0)
                .ThenByDescending(format => format.FileSize ?? 0)
                .First())
            .OrderByDescending(format => format.Height ?? 0)
            .ThenByDescending(format => NormalizeFps(format.Fps));

        foreach (var format in combined)
        {
            var height = format.Height!.Value;
            var fps = NormalizeFps(format.Fps);
            var fpsLabel = fps > 0 ? $"{fps}" : string.Empty;
            var extension = string.IsNullOrWhiteSpace(format.Extension)
                ? string.Empty
                : $" - {format.Extension.ToUpperInvariant()}";
            var label = $"{height}p{fpsLabel}{extension} - direct";

            choices.Add(new QualityChoice(
                QualityChoiceKind.ExactHeight,
                height,
                label,
                fps > 0 ? fps : null,
                format.FormatId,
                FormatHasAudio: true));
        }

        return choices;
    }

    public static string BuildStreamFormatSelector(QualityChoice? choice = null)
    {
        if (choice?.Kind == QualityChoiceKind.ExactHeight &&
            !string.IsNullOrWhiteSpace(choice.FormatId) &&
            choice.FormatHasAudio)
        {
            return choice.FormatId!;
        }

        if (choice?.Kind == QualityChoiceKind.ExactHeight &&
            choice.Height is > 0)
        {
            return $"best[height={choice.Height.Value}][vcodec!=none][acodec!=none]/" +
                   $"best[height<={choice.Height.Value}][vcodec!=none][acodec!=none]";
        }

        return "best[ext=mp4][vcodec!=none][acodec!=none]/best[vcodec!=none][acodec!=none]";
    }

    public async Task<string> DownloadAsync(
        MediaInfo media,
        QualityChoice? qualityChoice,
        AudioChoice? audioChoice,
        OutputFormatKind outputFormat,
        int mp3BitrateKbps,
        string outputDirectory,
        bool audioFadeInOut3Seconds = false,
        bool trimBoundarySilence = false,
        bool autoRemoveTikTokWatermark = true,
        string? outputTitleOverride = null,
        CancellationToken cancellationToken = default,
        Action<string>? onProgress = null,
        double? clipStartSeconds = null,
        double? clipEndSeconds = null)
    {
        Directory.CreateDirectory(outputDirectory);

        var ytDlp = RequireTool("yt-dlp.exe");
        var args = BuildCommonArguments();

        args.Add("--no-playlist");
        AddProgressArguments(args);
        args.Add("--paths");
        args.Add(outputDirectory);
        args.Add("--output");
        args.Add(BuildSingleOutputTemplate(outputTitleOverride));
        args.AddRange(BuildEmbeddedArtworkArguments());

        var target = NormalizeTarget(media.WebpageUrl);
        AddFormatArguments(
            args,
            outputFormat,
            qualityChoice,
            audioChoice,
            mp3BitrateKbps,
            audioFadeInOut3Seconds,
            trimBoundarySilence,
            autoRemoveTikTokWatermark && IsTikTokTarget(target.Url));

        var downloadSectionR1637 = BuildDownloadSectionSpecR1637(
            clipStartSeconds,
            clipEndSeconds,
            media.DurationSeconds);
        if (!string.IsNullOrWhiteSpace(downloadSectionR1637))
        {
            args.Add("--download-sections");
            args.Add(downloadSectionR1637);
        }

        args.Add(target.Url);

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
        IReadOnlyCollection<int> selectedIndexes,
        OutputFormatKind outputFormat,
        int mp3BitrateKbps,
        string outputDirectory,
        bool audioFadeInOut3Seconds = false,
        bool trimBoundarySilence = false,
        bool autoRemoveTikTokWatermark = true,
        CancellationToken cancellationToken = default,
        Action<string>? onProgress = null)
    {
        Directory.CreateDirectory(outputDirectory);

        if (selectedIndexes.Count == 0)
        {
            throw new ArgumentException("At least one playlist item must be selected.", nameof(selectedIndexes));
        }

        var ytDlp = RequireTool("yt-dlp.exe");
        var args = BuildCommonArguments();

        args.Add("--yes-playlist");
        args.Add("--ignore-errors");
        args.Add("--playlist-items");
        args.Add(BuildPlaylistItemSpec(selectedIndexes));
        AddProgressArguments(args);
        args.Add("--paths");
        args.Add(outputDirectory);
        args.Add("--output");
        args.Add("%(playlist_title)s/%(playlist_index)03d - %(title).180B [%(id)s].%(ext)s");
        args.AddRange(BuildEmbeddedArtworkArguments());

        AddFormatArguments(
            args,
            outputFormat,
            new QualityChoice(QualityChoiceKind.Best, null, "Auto - Best available per video"),
            null,
            mp3BitrateKbps,
            audioFadeInOut3Seconds,
            trimBoundarySilence,
            autoRemoveTikTokWatermark && IsTikTokTarget(playlist.WebpageUrl));

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

    // MEDIADOCK_DOWNLOAD_SECTION_R1637
    public static string? BuildDownloadSectionSpecR1637(
        double? startSeconds,
        double? endSeconds,
        double? durationSeconds)
    {
        var duration = durationSeconds is > 0 ? durationSeconds.Value : (double?)null;
        var start = Math.Max(0, startSeconds ?? 0);
        var end = endSeconds is > 0 ? endSeconds : duration;

        if (duration is not null && end is not null)
        {
            end = Math.Min(end.Value, duration.Value);
        }

        if (end is not null && end.Value <= start)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endSeconds),
                "Clip end must be later than clip start.");
        }

        var coversFullMedia = start <= 0.001 &&
            (end is null || (duration is not null && end.Value >= duration.Value - 0.25));
        if (coversFullMedia)
        {
            return null;
        }

        var startText = FormatSectionTimestampR1637(start);
        var endText = end is null ? "inf" : FormatSectionTimestampR1637(end.Value);
        return $"*{startText}-{endText}";
    }

    private static string FormatSectionTimestampR1637(double seconds)
    {
        var total = Math.Max(0L, (long)Math.Floor(seconds));
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var secondsPart = total % 60;
        return $"{hours:00}:{minutes:00}:{secondsPart:00}";
    }

    public static void RunClipRangeSelfTestR1637()
    {
        var fixture = BuildDownloadSectionSpecR1637(5, 157, 157);
        if (!string.Equals(fixture, "*00:00:05-00:02:37", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "R1.6.37 download-section contract failed for 00:05 through 02:37.");
        }

        if (BuildDownloadSectionSpecR1637(0, 157, 157) is not null)
        {
            throw new InvalidOperationException(
                "R1.6.37 download-section contract failed: full media should not invoke clipping.");
        }
    }

    public static IReadOnlyList<string> BuildEmbeddedArtworkArguments() =>
    [
        "--embed-thumbnail",
        "--convert-thumbnails", "jpg",
        "--embed-metadata"
    ];

    public static string BuildPlaylistItemSpec(IEnumerable<int> indexes)
    {
        var ordered = indexes
            .Where(index => index > 0)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        if (ordered.Length == 0)
        {
            throw new ArgumentException("At least one positive playlist index is required.", nameof(indexes));
        }

        var ranges = new List<string>();
        var start = ordered[0];
        var end = start;

        for (var i = 1; i < ordered.Length; i++)
        {
            var current = ordered[i];
            if (current == end + 1)
            {
                end = current;
                continue;
            }

            ranges.Add(start == end ? start.ToString(CultureInfo.InvariantCulture) : $"{start}-{end}");
            start = current;
            end = current;
        }

        ranges.Add(start == end ? start.ToString(CultureInfo.InvariantCulture) : $"{start}-{end}");
        return string.Join(",", ranges);
    }

    public static string BuildSingleOutputTemplate(string? queueTitle)
    {
        if (string.IsNullOrWhiteSpace(queueTitle))
        {
            return "%(title).180B [%(id)s].%(ext)s";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(queueTitle
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray());

        cleaned = cleaned.Replace("%", "%%", StringComparison.Ordinal).Trim();
        if (cleaned.Length > 140)
        {
            cleaned = cleaned[..140].Trim();
        }

        return string.IsNullOrWhiteSpace(cleaned)
            ? "%(title).180B [%(id)s].%(ext)s"
            : $"{cleaned} [%(id)s].%(ext)s";
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
                ? $" - {format.Width.Value}x{height}"
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
        // MEDIADOCK_YOUTUBE_AUDIO_TRACKS_DUBS_R1636
        static string FriendlyLanguage(string language)
        {
            var raw = (language ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw) ||
                string.Equals(raw, "und", StringComparison.OrdinalIgnoreCase))
            {
                return "Original audio";
            }

            try
            {
                return CultureInfo.GetCultureInfo(raw).EnglishName;
            }
            catch (CultureNotFoundException)
            {
                var baseCode = raw.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(baseCode))
                {
                    try
                    {
                        return CultureInfo.GetCultureInfo(baseCode).EnglishName;
                    }
                    catch (CultureNotFoundException)
                    {
                    }
                }

                return raw.ToUpperInvariant();
            }
        }

        static bool IsDubbed(MediaFormat format)
        {
            var note = format.FormatNote ?? string.Empty;
            return note.Contains("dub", StringComparison.OrdinalIgnoreCase) ||
                   note.Contains("translated", StringComparison.OrdinalIgnoreCase);
        }

        var audioFormats = media.Formats
            .Where(format =>
                format.HasAudio &&
                !format.HasVideo &&
                !format.IsDrm &&
                !string.IsNullOrWhiteSpace(format.FormatId))
            .ToArray();

        if (audioFormats.Length == 0)
        {
            return [new AudioChoice("Best available audio")];
        }

        var representatives = audioFormats
            .GroupBy(format => new
            {
                Language = NormalizeAudioLanguageR1630(format.Language),
                Original = IsOriginalAudioR1630(format),
                Default = IsDefaultAudioR1630(format),
                Dubbed = IsDubbed(format)
            })
            .Select(group => group
                .OrderByDescending(AudioPreferenceScoreR1630)
                .ThenByDescending(format => format.AudioBitrate ?? 0)
                .ThenByDescending(format => format.LanguagePreference)
                .First())
            .OrderByDescending(format => IsOriginalAudioR1630(format))
            .ThenByDescending(format => IsDefaultAudioR1630(format))
            .ThenBy(format => IsDubbed(format) ? 1 : 0)
            .ThenBy(format => FriendlyLanguage(format.Language), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var choices = new List<AudioChoice>(representatives.Length);
        foreach (var format in representatives)
        {
            var language = FriendlyLanguage(format.Language);
            var role = IsOriginalAudioR1630(format)
                ? "Original"
                : IsDubbed(format)
                    ? "Dub"
                    : IsDefaultAudioR1630(format)
                        ? "Default"
                        : "Alternate";

            var bitrate = format.AudioBitrate is > 0
                ? $" · {Math.Round(format.AudioBitrate.Value):0} kbps"
                : string.Empty;

            choices.Add(new AudioChoice(
                $"{language} — {role}{bitrate}",
                format.FormatId));
        }

        return choices.Count > 0
            ? choices
            : [new AudioChoice("Best available audio")];
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
            .Where(choice => choice.Height == 480)
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
        // The video's source/original language is always the default when yt-dlp exposes it.
        var all = choices.ToArray();
        return all.FirstOrDefault(choice =>
                   !string.IsNullOrWhiteSpace(choice.FormatId) &&
                   choice.Label.Contains("— Original", StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault(choice =>
                   !string.IsNullOrWhiteSpace(choice.FormatId) &&
                   choice.Label.Contains("— Default", StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault(choice => !string.IsNullOrWhiteSpace(choice.FormatId))
            ?? all.FirstOrDefault();
    }

    public static string NormalizeUserUrl(string url) => NormalizeTarget(url).Url;

    // MEDIADOCK_SUBTITLE_DOWNLOAD_R1628
    public async Task<IReadOnlyList<string>> DownloadSubtitlesAsync(
        MediaInfo media,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);

        Directory.CreateDirectory(outputDirectory);

        var ytDlp =
            RequireTool("yt-dlp.exe");

        var args =
            BuildCommonArguments();

        AddSubtitleArgumentsR1628(
            args,
            outputDirectory);

        args.Add(
            NormalizeTarget(media.WebpageUrl).Url);

        var startedUtc =
            DateTime.UtcNow.AddSeconds(-2);

        var result =
            await _runner.RunAsync(
                ytDlp,
                args,
                cancellationToken)
                .ConfigureAwait(false);

        if (!result.Success)
        {
            throw new MediaEngineException(
                "Subtitle download failed.",
                BuildDiagnostics(result));
        }

        var files =
            Directory.EnumerateFiles(
                outputDirectory,
                "*",
                SearchOption.TopDirectoryOnly)
                .Where(path =>
                    IsSubtitleFileR1628(path) &&
                    File.GetLastWriteTimeUtc(path) >= startedUtc)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (files.Length == 0)
        {
            throw new MediaEngineException(
                "No subtitles or automatic captions were available for this media.",
                BuildDiagnostics(result));
        }

        return files;
    }

    private static void AddSubtitleArgumentsR1628(
        List<string> args,
        string outputDirectory)
    {
        args.Add("--no-playlist");
        args.Add("--skip-download");
        args.Add("--write-subs");
        args.Add("--write-auto-subs");
        args.Add("--sub-langs");
        args.Add("all,-live_chat");
        args.Add("--convert-subs");
        args.Add("srt");
        args.Add("--force-overwrites");
        args.Add("--paths");
        args.Add(outputDirectory);
        args.Add("--output");
        args.Add("%(title).180B [%(id)s].%(ext)s");
    }

    private static bool IsSubtitleFileR1628(string path)
    {
        var extension =
            Path.GetExtension(path);

        return
            extension.Equals(".srt", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".vtt", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ass", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".lrc", StringComparison.OrdinalIgnoreCase);
    }

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

    // MEDIADOCK_AUDIO_OUTPUT_FORMATS_R1639
    public static string? GetExtractAudioFormatR1639(OutputFormatKind outputFormat) =>
        outputFormat switch
        {
            OutputFormatKind.Mp3 => "mp3",
            OutputFormatKind.M4a => "m4a",
            OutputFormatKind.Flac => "flac",
            _ => null
        };

    public static void RunAudioOutputFormatSelfTestR1639()
    {
        if (!string.Equals(GetExtractAudioFormatR1639(OutputFormatKind.Mp3), "mp3", StringComparison.Ordinal) ||
            !string.Equals(GetExtractAudioFormatR1639(OutputFormatKind.M4a), "m4a", StringComparison.Ordinal) ||
            !string.Equals(GetExtractAudioFormatR1639(OutputFormatKind.Flac), "flac", StringComparison.Ordinal) ||
            GetExtractAudioFormatR1639(OutputFormatKind.Mp4) is not null)
        {
            throw new InvalidOperationException("R1.6.39 audio output-format mapping contract failed.");
        }
    }

    private static void AddFormatArguments(
        List<string> args,
        OutputFormatKind outputFormat,
        QualityChoice? qualityChoice,
        AudioChoice? audioChoice,
        int mp3BitrateKbps,
        bool audioFadeInOut3Seconds,
        bool trimBoundarySilence,
        bool preferTikTokWatermarkFree)
    {
        var extractAudioFormatR1639 = GetExtractAudioFormatR1639(outputFormat);
        if (!string.IsNullOrWhiteSpace(extractAudioFormatR1639))
        {
            args.Add("--format");
            args.Add(string.IsNullOrWhiteSpace(audioChoice?.FormatId) ? "bestaudio[format_note*=original]/bestaudio[language^=en]/bestaudio[format_note*=default]/bestaudio/best" : audioChoice!.FormatId!);
            args.Add("--extract-audio");
            args.Add("--audio-format");
            args.Add(extractAudioFormatR1639);
            if (outputFormat == OutputFormatKind.Mp3)
            {
                args.Add("--audio-quality");
                args.Add(string.Format(CultureInfo.InvariantCulture, "{0}K", mp3BitrateKbps));
            }

            var audioFilter = FfmpegConversionService.BuildAudioFilter(
                audioFadeInOut3Seconds,
                trimBoundarySilence);
            if (!string.IsNullOrWhiteSpace(audioFilter))
            {
                args.Add("--postprocessor-args");
                args.Add($"ExtractAudio+ffmpeg_o:-af {audioFilter}");
            }

            return;
        }

        args.Add("--format");
        args.Add(BuildFormatSelector(qualityChoice, audioChoice, preferTikTokWatermarkFree));
        args.Add("--merge-output-format");
        args.Add(OutputFormatPolicyR1641.MergeContainer(outputFormat));
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
        // MEDIADOCK_DOWNLOAD_ACCELERATION_R1620_BEGIN
        // IDM-style acceleration for segmented sources. yt-dlp can fetch up to
        // eight media fragments concurrently when the server/source supports it.
        args.Add("--concurrent-fragments");
        args.Add("8");
        args.Add("--buffer-size");
        args.Add("1M");
        args.Add("--http-chunk-size");
        args.Add("10M");
        args.Add("--retries");
        args.Add("10");
        args.Add("--fragment-retries");
        args.Add("10");
        // MEDIADOCK_DOWNLOAD_ACCELERATION_R1620_END
return args;
    }

    private static string BuildFormatSelector(
        QualityChoice? choice,
        AudioChoice? audioChoice,
        bool preferTikTokWatermarkFree)
    {
        var audioSelector = string.IsNullOrWhiteSpace(audioChoice?.FormatId)
            ? "(bestaudio[format_note*=original]/bestaudio[language^=en]/bestaudio[format_note*=default]/bestaudio)"
            : audioChoice!.FormatId!;

        if (preferTikTokWatermarkFree)
        {
            // TikTok commonly exposes the explicitly watermarked stream under
            // the "download" format ID. Prefer other exposed streams first, but
            // keep the original selector as a final fallback so the setting can
            // never turn a supported TikTok URL into an avoidable hard failure.
            var safeAudio = string.IsNullOrWhiteSpace(audioChoice?.FormatId)
                ? "bestaudio"
                : audioChoice!.FormatId!;

            if (choice?.Kind == QualityChoiceKind.ExactHeight && choice.Height is > 0)
            {
                return $"bestvideo[height={choice.Height.Value}][format_id!=download]+{safeAudio}/best[height={choice.Height.Value}][format_id!=download]/bestvideo[height={choice.Height.Value}]+{safeAudio}/best";
            }

            return $"bestvideo[format_id!=download]+{safeAudio}/best[format_id!=download]/bestvideo+{safeAudio}/best";
        }

        if (choice is null)
        {
            return $"bestvideo+{audioSelector}/best";
        }

        return choice.Kind switch
        {
            QualityChoiceKind.Best => $"bestvideo+{audioSelector}/best",
            QualityChoiceKind.AudioOnly => BuildPreferredAudioSelectorR1630(audioChoice),
            // MEDIADOCK_EXPLICIT_AUDIO_TRACK_OVERRIDE_R1636
            // If the user chose a specific original/dub track, never let a combined
            // video format silently replace that selection with its embedded audio.
            QualityChoiceKind.ExactHeight when
                !string.IsNullOrWhiteSpace(audioChoice?.FormatId) &&
                choice.Height is > 0 =>
                $"bestvideo[height={choice.Height.Value}]+{audioSelector}/" +
                $"bestvideo[height<={choice.Height.Value}]+{audioSelector}/" +
                $"bestvideo+{audioSelector}/best",
            QualityChoiceKind.ExactHeight when !string.IsNullOrWhiteSpace(choice.FormatId) && choice.FormatHasAudio =>
                choice.FormatId!,
            QualityChoiceKind.ExactHeight when !string.IsNullOrWhiteSpace(choice.FormatId) =>
                $"{choice.FormatId}+{audioSelector}",
            QualityChoiceKind.ExactHeight when choice.Height is > 0 =>
                $"bestvideo[height={choice.Height.Value}]+{audioSelector}",
            _ => throw new InvalidOperationException("Invalid quality selection.")
        };
    }

    public static bool IsTikTokUrl(string url) => IsTikTokTarget(url);

    private static bool IsTikTokTarget(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        return host == "tiktok.com" ||
               host.EndsWith(".tiktok.com", StringComparison.Ordinal) ||
               host == "tiktokv.com" ||
               host.EndsWith(".tiktokv.com", StringComparison.Ordinal);
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
            // MEDIADOCK_PUBLIC_MEDIA_NORMALIZATION_R1628
            // Known single-post/public-media hosts should receive full format
            // extraction rather than flat collection discovery.
            if (IsKnownSingleMediaTargetR1628(uri))
            {
                return new MediaTarget(trimmed, true, false);
            }

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

    // MEDIADOCK_PUBLIC_MEDIA_HELPERS_R1628
    private static bool IsKnownSingleMediaTargetR1628(Uri uri)
    {
        var host =
            uri.Host.ToLowerInvariant();

        var path =
            uri.AbsolutePath;

        if (host == "redd.it" ||
            host == "v.redd.it")
        {
            return true;
        }

        if ((host == "reddit.com" ||
             host.EndsWith(".reddit.com", StringComparison.Ordinal)) &&
            path.Contains("/comments/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (host == "tiktok.com" ||
            host.EndsWith(".tiktok.com", StringComparison.Ordinal) ||
            host == "instagram.com" ||
            host.EndsWith(".instagram.com", StringComparison.Ordinal) ||
            host == "facebook.com" ||
            host.EndsWith(".facebook.com", StringComparison.Ordinal) ||
            host == "fb.watch" ||
            host == "x.com" ||
            host.EndsWith(".x.com", StringComparison.Ordinal) ||
            host == "twitter.com" ||
            host.EndsWith(".twitter.com", StringComparison.Ordinal) ||
            host == "vimeo.com" ||
            host.EndsWith(".vimeo.com", StringComparison.Ordinal) ||
            host == "dailymotion.com" ||
            host.EndsWith(".dailymotion.com", StringComparison.Ordinal) ||
            host == "streamable.com" ||
            host.EndsWith(".streamable.com", StringComparison.Ordinal) ||
            host == "clips.twitch.tv")
        {
            return true;
        }

        var extension =
            Path.GetExtension(path);

        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".m3u8", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mpd", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".aac", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase);
    }

    public static void RunPublicMediaContractSelfTestR1628()
    {
        var reddit =
            NormalizeTarget(
                "https://www.reddit.com/r/videos/comments/abc123/example/");

        if (!reddit.ForceSingleVideo ||
            reddit.ForcePlaylist)
        {
            throw new InvalidOperationException(
                "Reddit single-post normalization contract failed.");
        }

        var redditDirect =
            NormalizeTarget(
                "https://v.redd.it/abc123/DASH_720.mp4");

        if (!redditDirect.ForceSingleVideo)
        {
            throw new InvalidOperationException(
                "Reddit direct-media normalization contract failed.");
        }

        var genericDirect =
            NormalizeTarget(
                "https://cdn.example.test/video.mp4");

        if (!genericDirect.ForceSingleVideo)
        {
            throw new InvalidOperationException(
                "Generic direct-media normalization contract failed.");
        }

        var youtubePlaylist =
            NormalizeTarget(
                "https://www.youtube.com/playlist?list=PL12345");

        if (youtubePlaylist.ForceSingleVideo ||
            !youtubePlaylist.ForcePlaylist)
        {
            throw new InvalidOperationException(
                "YouTube explicit-playlist normalization regressed.");
        }

        var args =
            new List<string>();

        AddSubtitleArgumentsR1628(
            args,
            @"C:\Temp");

        foreach (var required in new[]
        {
            "--skip-download",
            "--write-subs",
            "--write-auto-subs",
            "--sub-langs",
            "all,-live_chat",
            "--convert-subs",
            "srt"
        })
        {
            if (!args.Contains(
                required,
                StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Subtitle argument contract missing: " +
                    required);
            }
        }
    }

    // MEDIADOCK_AUDIO_HELPERS_CLASS_SCOPED_R1630
    public static string BuildPreferredAudioSelectorR1630(AudioChoice? audioChoice)
    {
        if (!string.IsNullOrWhiteSpace(audioChoice?.FormatId))
        {
            return audioChoice!.FormatId!;
        }

        // Do not trust a stable YouTube audio format number: multi-audio IDs can
        // map to different dubbed languages between videos/yt-dlp releases.
        return "bestaudio[format_note*=original]/bestaudio[language^=en]/bestaudio[format_note*=default]/bestaudio/best";
    }

    private static string NormalizeAudioLanguageR1630(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static bool IsEnglishAudioR1630(MediaFormat format)
    {
        var language = NormalizeAudioLanguageR1630(format.Language);
        return language == "en" || language.StartsWith("en-", StringComparison.Ordinal);
    }

    private static bool IsOriginalAudioR1630(MediaFormat format) =>
        (format.FormatNote ?? string.Empty).Contains("original", StringComparison.OrdinalIgnoreCase);

    private static bool IsDefaultAudioR1630(MediaFormat format) =>
        (format.FormatNote ?? string.Empty).Contains("default", StringComparison.OrdinalIgnoreCase);

    private static int AudioPreferenceScoreR1630(MediaFormat format)
    {
        var score = 0;
        if (IsOriginalAudioR1630(format)) score += 100000;
        if (IsEnglishAudioR1630(format)) score += 10000;
        if (IsDefaultAudioR1630(format)) score += 1000;
        score += Math.Clamp(format.LanguagePreference, -100, 100);
        return score;
    }

    public static void RunAudioTrackSelectionSelfTestR1636()
    {
        static MediaFormat Audio(
            string id,
            double bitrate,
            string language,
            string note,
            int preference) =>
            new(
                FormatId: id,
                Width: null,
                Height: null,
                Fps: null,
                Extension: "m4a",
                VideoCodec: "none",
                AudioCodec: "mp4a.40.2",
                Protocol: "https",
                FileSize: null,
                VideoBitrate: null,
                AudioBitrate: bitrate,
                HasVideo: false,
                HasAudio: true,
                IsDrm: false,
                Language: language,
                FormatNote: note,
                LanguagePreference: preference);

        var media = new MediaInfo(
            Id: "fixture",
            Title: "Audio track fixture",
            WebpageUrl: "https://www.youtube.com/watch?v=fixture",
            Extractor: "Youtube",
            Uploader: "Fixture",
            DurationSeconds: 60,
            ThumbnailUrl: string.Empty,
            Formats:
            [
                Audio("es-original", 128, "es", "Spanish original (default)", 0),
                Audio("en-dub", 192, "en", "English dubbed audio", 50),
                Audio("ja-dub", 160, "ja", "Japanese dubbed audio", 40)
            ]);

        var choices = BuildAudioChoices(media);
        var selected = SelectPreferredDefaultAudio(choices);

        if (!string.Equals(selected?.FormatId, "es-original", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "R1.6.36 audio-track contract failed: the video's original language was not selected by default.");
        }

        if (!choices.Any(choice =>
                string.Equals(choice.FormatId, "en-dub", StringComparison.Ordinal) &&
                choice.Label.Contains("English", StringComparison.OrdinalIgnoreCase) &&
                choice.Label.Contains("Dub", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "R1.6.36 audio-track contract failed: an exposed English dub was not presented as a selectable dub.");
        }

        var englishDub = choices.First(choice =>
            string.Equals(choice.FormatId, "en-dub", StringComparison.Ordinal));

        var explicitSelector = BuildFormatSelector(
            new QualityChoice(
                QualityChoiceKind.ExactHeight,
                1080,
                "1080p",
                30,
                "combined-1080",
                FormatHasAudio: true),
            englishDub,
            preferTikTokWatermarkFree: false);

        if (!explicitSelector.Contains("en-dub", StringComparison.Ordinal) ||
            !explicitSelector.Contains("bestvideo", StringComparison.Ordinal) ||
            string.Equals(explicitSelector, "combined-1080", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "R1.6.36 audio-track contract failed: explicit dub selection was bypassed by combined video audio.");
        }
    }
    public static void RunAudioLanguagePreferenceSelfTestR1630()
    {
        static MediaFormat Audio(
            string id,
            double bitrate,
            string language,
            string note,
            int languagePreference = 0) =>
            new(
                id,
                null,
                null,
                null,
                "m4a",
                "none",
                "mp4a.40.2",
                "https",
                null,
                null,
                bitrate,
                false,
                true,
                false,
                language,
                note,
                languagePreference);

        var englishSource = new MediaInfo(
            "fixture-en",
            "English source with auto-dubs",
            "https://www.youtube.com/watch?v=fixture-en",
            "Youtube",
            "fixture",
            null,
            string.Empty,
            new[]
            {
                Audio("ja-dub", 192, "ja", "Japanese dubbed audio", 50),
                Audio("es-dub", 160, "es", "Spanish dubbed audio", 40),
                Audio("en-original", 128, "en-US", "English (US) original (default)", 0)
            });

        var preferredEnglish = SelectPreferredDefaultAudio(BuildAudioChoices(englishSource));
        if (!string.Equals(preferredEnglish?.FormatId, "en-original", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("R1.6.30 audio-language contract failed: English original was not preferred over higher-bitrate dubbed tracks.");
        }

        var nonEnglishSource = new MediaInfo(
            "fixture-es",
            "Spanish original with English dub",
            "https://www.youtube.com/watch?v=fixture-es",
            "Youtube",
            "fixture",
            null,
            string.Empty,
            new[]
            {
                Audio("en-dub", 192, "en", "English dubbed audio", 50),
                Audio("es-original", 128, "es", "Spanish original (default)", 0)
            });

        var preferredOriginal = SelectPreferredDefaultAudio(BuildAudioChoices(nonEnglishSource));
        if (!string.Equals(preferredOriginal?.FormatId, "es-original", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("R1.6.30 audio-language contract failed: original-language audio did not outrank an English dub.");
        }

        var fallback = BuildPreferredAudioSelectorR1630(new AudioChoice("Best available audio"));
        foreach (var token in new[] { "format_note*=original", "language^=en", "format_note*=default", "bestaudio/best" })
        {
            if (!fallback.Contains(token, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("R1.6.30 audio fallback selector is missing: " + token);
            }
        }
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
                var thumbnail = GetPlaylistEntryThumbnail(entry, id, webpageUrl);
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

    private static string GetPlaylistEntryThumbnail(JsonElement entry, string id, string webpageUrl)
    {
        var direct = GetString(entry, "thumbnail", string.Empty).Trim();
        if (Uri.TryCreate(direct, UriKind.Absolute, out var directUri) &&
            directUri.Scheme is "http" or "https")
        {
            return directUri.AbsoluteUri;
        }

        if (entry.TryGetProperty("thumbnails", out var thumbnails) &&
            thumbnails.ValueKind == JsonValueKind.Array)
        {
            string best = string.Empty;
            long bestArea = -1;

            foreach (var thumbnail in thumbnails.EnumerateArray())
            {
                if (thumbnail.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var url = GetString(thumbnail, "url", string.Empty).Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not "http" and not "https")
                {
                    continue;
                }

                var width = GetLong(thumbnail, "width") ?? 0;
                var height = GetLong(thumbnail, "height") ?? 0;
                var area = width > 0 && height > 0 ? width * height : 0;
                if (area >= bestArea)
                {
                    bestArea = area;
                    best = uri.AbsoluteUri;
                }
            }

            if (!string.IsNullOrWhiteSpace(best))
            {
                return best;
            }
        }

        // Flat YouTube playlist metadata can omit thumbnail fields while still
        // returning a stable video ID. The public hqdefault endpoint gives the
        // queue a reliable preview without forcing per-item analysis up front.
        var looksLikeYouTube =
            webpageUrl.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            webpageUrl.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(webpageUrl);

        if (looksLikeYouTube &&
            !string.IsNullOrWhiteSpace(id) &&
            id.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'))
        {
            return $"https://i.ytimg.com/vi/{id}/hqdefault.jpg";
        }

        return string.Empty;
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
                IsDrm: drm,
                Language: GetString(format, "language"),
                FormatNote: GetString(format, "format_note"),
                LanguagePreference: GetInt(format, "language_preference") ?? 0));
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
