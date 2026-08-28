using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using MediaDownloader.Core.Services;

namespace MediaDownloader.Core.Models;

// MEDIADOCK_FUNCTIONAL_QUEUE_ITEM_R1641
public sealed partial class DownloadQueueItem
{
    private string? _selectedClipModeR1641;

    [JsonIgnore]
    public IReadOnlyList<string> AvailableOutputFormatsR1641 =>
        OutputFormatPolicyR1641.AvailableQueueOutputLabels;

    [JsonIgnore]
    public string SelectedFormatR1641
    {
        get => OutputFormatPolicyR1641.ToLabel(OutputKind);
        set
        {
            var requested = OutputFormatPolicyR1641.ParseLabel(value);
            if (OutputKind != requested)
            {
                OutputKind = requested;
            }

            Format = OutputFormatPolicyR1641.ToLabel(requested);

            if (OutputFormatPolicyR1641.IsAudio(requested))
            {
                QualityChoice = null;
                Quality = requested switch
                {
                    OutputFormatKind.Mp3 => $"Best audio - {Mp3BitrateKbps} kbps",
                    OutputFormatKind.Flac => "Lossless / best",
                    _ => "Best available audio"
                };
            }
            else if (!AvailableQualityOptionsR1641.Contains(
                         SelectedQualityR1641,
                         StringComparer.Ordinal))
            {
                SelectedQualityR1641 = "Highest";
            }

            OnPropertyChanged(nameof(SelectedFormatR1641));
            OnPropertyChanged(nameof(AvailableQualityOptionsR1641));
            OnPropertyChanged(nameof(SelectedQualityR1641));
        }
    }

    [JsonIgnore]
    public IReadOnlyList<string> AvailableQualityOptionsR1641 =>
        OutputFormatPolicyR1641.BuildQueueQualityLabels(MediaSnapshot, OutputKind);

    [JsonIgnore]
    public string SelectedQualityR1641
    {
        get => OutputKind switch
        {
            OutputFormatKind.Mp3 => $"{Mp3BitrateKbps} kbps",
            OutputFormatKind.M4a => "Best available",
            OutputFormatKind.Flac => "Lossless / best",
            _ => SelectedQualityR1629
        };
        set
        {
            if (OutputKind == OutputFormatKind.Mp3)
            {
                Mp3BitrateKbps = OutputFormatPolicyR1641.ParseMp3Bitrate(
                    value,
                    Mp3BitrateKbps is 128 or 192 or 256 or 320
                        ? Mp3BitrateKbps
                        : 320);
                QualityChoice = null;
                Quality = $"Best audio - {Mp3BitrateKbps} kbps";
            }
            else if (OutputKind == OutputFormatKind.M4a)
            {
                QualityChoice = null;
                Quality = "Best available audio";
            }
            else if (OutputKind == OutputFormatKind.Flac)
            {
                QualityChoice = null;
                Quality = "Lossless / best";
            }
            else
            {
                SelectedQualityR1629 = value;
            }

            OnPropertyChanged(nameof(SelectedQualityR1641));
        }
    }

    [JsonIgnore]
    public bool CanEditQueuedOptionsR1641 => CanStart;

    [JsonIgnore]
    public string QueueInteractionHintR1641
    {
        get
        {
            if (Completed)
            {
                return OutputFileAvailable
                    ? "Completed. Use Open, Convert, Refresh, or Remove."
                    : "The previous output is unavailable. Refresh the row to reconcile it.";
            }

            if (Status.Contains("Downloading", StringComparison.OrdinalIgnoreCase) ||
                Status.Contains("Starting", StringComparison.OrdinalIgnoreCase) ||
                Status.Contains("Analyzing", StringComparison.OrdinalIgnoreCase) ||
                Status.Contains("Converting", StringComparison.OrdinalIgnoreCase))
            {
                return "Format, quality, audio, and clip settings are locked while this item is active.";
            }

            if (string.Equals(Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return "The last attempt failed. Change options if needed, then click Download to retry.";
            }

            if (string.Equals(Status, "Partial found", StringComparison.OrdinalIgnoreCase))
            {
                return "A partial output exists. Click Download to resume/retry this item.";
            }

            if (string.Equals(Status, "Missing", StringComparison.OrdinalIgnoreCase))
            {
                return "The previous output is missing. This item is ready to download again.";
            }

            return "Ready. Format, quality, audio/dub, and clip settings are editable.";
        }
    }

    [JsonIgnore]
    public string SelectedClipModeR1641
    {
        get => _selectedClipModeR1641 ?? InferClipModeR1641();
        set
        {
            var normalized = string.Equals(
                    value?.Trim(),
                    "Custom",
                    StringComparison.OrdinalIgnoreCase)
                ? "Custom"
                : "Full";

            if (!SetProperty(ref _selectedClipModeR1641, normalized))
            {
                return;
            }

            if (string.Equals(normalized, "Full", StringComparison.Ordinal))
            {
                ClipStartText = "00:00";
                ClipEndText = DurationSeconds is > 0 ? DurationText : string.Empty;
            }

            OnPropertyChanged(nameof(IsCustomClipR1641));
            OnPropertyChanged(nameof(SelectedClipModeR1641));
        }
    }

    [JsonIgnore]
    public bool IsCustomClipR1641 =>
        string.Equals(SelectedClipModeR1641, "Custom", StringComparison.Ordinal);

    [JsonIgnore]
    public string StatusDetailTextR1641
    {
        get
        {
            if (Status.Contains("Downloading", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(SpeedText))
            {
                return SpeedText;
            }

            if (Completed && OutputFileAvailable && !string.IsNullOrWhiteSpace(OutputPath))
            {
                return Path.GetFileName(OutputPath);
            }

            if (string.Equals(Status, "Partial found", StringComparison.OrdinalIgnoreCase))
            {
                return "Resume available";
            }

            if (string.Equals(Status, "Missing", StringComparison.OrdinalIgnoreCase))
            {
                return "Ready to download again";
            }

            if (string.Equals(Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return "Click Download to retry";
            }

            return string.Empty;
        }
    }

    private string InferClipModeR1641()
    {
        if (!TryResolveClipRangeR1637(out var start, out var end, out _))
        {
            return "Custom";
        }

        if (start > 0.001)
        {
            return "Custom";
        }

        if (DurationSeconds is > 0 &&
            end is not null &&
            end.Value < DurationSeconds.Value - 0.25)
        {
            return "Custom";
        }

        return "Full";
    }

    public static void RunFunctionalGuiSelfTestR1641()
    {
        var ready = new DownloadQueueItem(
            "Fixture",
            "YouTube",
            "https://www.youtube.com/watch?v=fixture",
            "1080p",
            "MP4",
            OutputFormatKind.Mp4,
            string.Empty);

        if (!ready.CanStart || !ready.CanEditQueuedOptionsR1641)
        {
            throw new InvalidOperationException(
                "R1.6.41 ready-row interaction contract failed.");
        }

        ready.Status = "Failed";
        if (!ready.CanStart || !ready.CanEditQueuedOptionsR1641)
        {
            throw new InvalidOperationException(
                "R1.6.41 failed-row retry contract failed.");
        }

        ready.IsSelectedR1629 = true;
        if (!ready.IsSelected)
        {
            throw new InvalidOperationException(
                "R1.6.41 queue-selection alias contract failed.");
        }

        ready.SelectedFormatR1641 = "MKV";
        if (ready.OutputKind != OutputFormatKind.Mkv ||
            !string.Equals(ready.Format, "MKV", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "R1.6.41 MKV row-selection contract failed.");
        }

        ready.SelectedFormatR1641 = "MP3";
        ready.SelectedQualityR1641 = "256 kbps";
        if (ready.OutputKind != OutputFormatKind.Mp3 ||
            ready.Mp3BitrateKbps != 256)
        {
            throw new InvalidOperationException(
                "R1.6.41 MP3 bitrate row-selection contract failed.");
        }

        ready.SelectedClipModeR1641 = "Custom";
        if (!ready.IsCustomClipR1641)
        {
            throw new InvalidOperationException(
                "R1.6.41 custom-clip mode contract failed.");
        }

        ready.SelectedClipModeR1641 = "Full";
        if (ready.IsCustomClipR1641 ||
            !string.Equals(ready.ClipStartText, "00:00", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "R1.6.41 full-media clip mode contract failed.");
        }
    }
}
