using System;
using System.Collections.Generic;
using System.Linq;
using MediaDownloader.Core.Models;

namespace MediaDownloader.Core.Services;

// MEDIADOCK_FUNCTIONAL_GUI_POLICY_R1641
public static class OutputFormatPolicyR1641
{
    private static readonly string[] QueueOutputLabels =
        ["MP4", "MKV", "MP3", "M4A", "FLAC"];

    private static readonly string[] Mp3QualityLabels =
        ["320 kbps", "256 kbps", "192 kbps", "128 kbps"];

    private static readonly string[] M4aQualityLabels =
        ["Best available"];

    private static readonly string[] FlacQualityLabels =
        ["Lossless / best"];

    private static readonly string[] UnknownVideoQualityLabels =
        ["Highest"];

    public static IReadOnlyList<string> AvailableQueueOutputLabels => QueueOutputLabels;

    public static bool IsAudio(OutputFormatKind kind) =>
        kind is OutputFormatKind.Mp3 or OutputFormatKind.M4a or OutputFormatKind.Flac;

    public static string ToLabel(OutputFormatKind kind) => kind switch
    {
        OutputFormatKind.Mkv => "MKV",
        OutputFormatKind.Mp3 => "MP3",
        OutputFormatKind.M4a => "M4A",
        OutputFormatKind.Flac => "FLAC",
        _ => "MP4"
    };

    public static OutputFormatKind ParseLabel(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "MKV" => OutputFormatKind.Mkv,
            "MP3" => OutputFormatKind.Mp3,
            "M4A" => OutputFormatKind.M4a,
            "FLAC" => OutputFormatKind.Flac,
            _ => OutputFormatKind.Mp4
        };

    public static string MergeContainer(OutputFormatKind kind) =>
        kind == OutputFormatKind.Mkv ? "mkv" : "mp4";

    public static IReadOnlyList<string> BuildQueueQualityLabels(
        MediaInfo? media,
        OutputFormatKind outputKind)
    {
        if (outputKind == OutputFormatKind.Mp3)
        {
            return Mp3QualityLabels;
        }

        if (outputKind == OutputFormatKind.M4a)
        {
            return M4aQualityLabels;
        }

        if (outputKind == OutputFormatKind.Flac)
        {
            return FlacQualityLabels;
        }

        if (media is null)
        {
            return UnknownVideoQualityLabels;
        }

        var heights = media.Formats
            .Where(format =>
                format.HasVideo &&
                !format.IsDrm &&
                format.Height is > 0)
            .Select(format => format.Height!.Value)
            .Distinct()
            .OrderByDescending(height => height)
            .ToArray();

        var values = new List<string>(heights.Length + 1)
        {
            "Highest"
        };
        values.AddRange(heights.Select(height => $"{height}p"));
        return values;
    }

    public static int ParseMp3Bitrate(string? value, int fallback = 320)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var bitrate) &&
               bitrate is 128 or 192 or 256 or 320
            ? bitrate
            : fallback;
    }

    public static void RunSelfTestR1641()
    {
        var formats = new[]
        {
            new MediaFormat(
                "137", 1920, 1080, 30, "mp4", "avc1", "none", "https",
                null, null, null, true, false, false),
            new MediaFormat(
                "136", 1280, 720, 30, "mp4", "avc1", "none", "https",
                null, null, null, true, false, false),
            new MediaFormat(
                "140", null, null, null, "m4a", "none", "mp4a", "https",
                null, null, 128, false, true, false)
        };

        var media = new MediaInfo(
            "fixture",
            "Fixture",
            "https://www.youtube.com/watch?v=fixture",
            "YouTube",
            "Fixture",
            120,
            string.Empty,
            formats);

        var videoQualities = BuildQueueQualityLabels(media, OutputFormatKind.Mp4);
        if (!videoQualities.SequenceEqual(["Highest", "1080p", "720p"], StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "R1.6.41 source-supported quality contract failed.");
        }

        if (BuildQueueQualityLabels(media, OutputFormatKind.Mp3).Count != 4 ||
            ParseMp3Bitrate("256 kbps") != 256 ||
            ParseLabel("MKV") != OutputFormatKind.Mkv ||
            !string.Equals(MergeContainer(OutputFormatKind.Mkv), "mkv", StringComparison.Ordinal) ||
            !string.Equals(MergeContainer(OutputFormatKind.Mp4), "mp4", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "R1.6.41 output-format policy contract failed.");
        }
    }
}
