using MediaDownloader.Infrastructure;

namespace MediaDownloader.Core.Models;

public sealed class DownloadQueueItem : ObservableObject
{
    private string _title;
    private double _progressPercent;
    private string _status = "Ready";
    private string _progressText = string.Empty;
    private string _speedText = string.Empty;
    private string _outputPath = string.Empty;
    private bool _completed;
    private string _quality;
    private string _format;
    private OutputFormatKind _outputKind;
    private int _mp3BitrateKbps;
    private QualityChoice? _qualityChoice;
    private AudioChoice? _audioChoice;
    private MediaInfo? _mediaSnapshot;
    private bool _hasDownloadedMp3Counterpart;
    private bool _hasDownloadedMp4Counterpart;
    private bool _outputFileAvailable;
    private bool _isSelected;
    private string _thumbnailUrl;
    // MEDIADOCK_CLIP_RANGE_R1637
    private double? _durationSeconds;
    private string _clipStartText = "00:00";
    private string _clipEndText = string.Empty;

    public DownloadQueueItem(
        string title,
        string source,
        string sourceUrl,
        string quality,
        string format,
        OutputFormatKind outputKind,
        string thumbnailUrl,
        int mp3BitrateKbps = 320)
    {
        _title = title;
        OriginalTitle = title;
        Source = source;
        SourceUrl = sourceUrl;
        _quality = quality;
        _format = format;
        _outputKind = outputKind;
        _thumbnailUrl = thumbnailUrl ?? string.Empty;
        _mp3BitrateKbps = mp3BitrateKbps;
    }

    public string Title
    {
        get => _title;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? OriginalTitle : value.Trim();
            SetProperty(ref _title, normalized);
        }
    }

    public string OriginalTitle { get; }
    public string Source { get; }
    public string SourceUrl { get; }

    public string ThumbnailUrl
    {
        get => _thumbnailUrl;
        set => SetProperty(ref _thumbnailUrl, value ?? string.Empty);
    }

    // MEDIADOCK_CLIP_RANGE_R1637
    public double? DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            var normalized = value is > 0 ? value : null;
            if (SetProperty(ref _durationSeconds, normalized))
            {
                OnPropertyChanged(nameof(DurationText));
                OnPropertyChanged(nameof(DurationDisplayText));
                if (normalized is > 0 && string.IsNullOrWhiteSpace(_clipEndText))
                {
                    ClipEndText = FormatTimestampR1637(normalized.Value);
                }
            }
        }
    }

    public string DurationText => DurationSeconds is > 0
        ? FormatTimestampR1637(DurationSeconds.Value)
        : "--:--";

    public string DurationDisplayText => $"Length {DurationText}";

    public string ClipStartText
    {
        get => _clipStartText;
        set => SetProperty(ref _clipStartText, string.IsNullOrWhiteSpace(value) ? "00:00" : value.Trim());
    }

    public string ClipEndText
    {
        get => _clipEndText;
        set => SetProperty(ref _clipEndText, value?.Trim() ?? string.Empty);
    }

    public bool TryResolveClipRangeR1637(
        out double startSeconds,
        out double? endSeconds,
        out string error)
    {
        startSeconds = 0;
        endSeconds = null;
        error = string.Empty;

        if (!TryParseTimestampR1637(ClipStartText, out startSeconds))
        {
            error = "Start time must use mm:ss or hh:mm:ss.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ClipEndText))
        {
            endSeconds = DurationSeconds;
        }
        else if (TryParseTimestampR1637(ClipEndText, out var parsedEnd))
        {
            endSeconds = parsedEnd;
        }
        else
        {
            error = "End time must use mm:ss or hh:mm:ss.";
            return false;
        }

        if (DurationSeconds is > 0)
        {
            if (startSeconds >= DurationSeconds.Value)
            {
                error = $"Start time must be earlier than {DurationText}.";
                return false;
            }

            if (endSeconds is > 0 && endSeconds.Value > DurationSeconds.Value + 0.5)
            {
                error = $"End time cannot be later than {DurationText}.";
                return false;
            }

            if (endSeconds is > 0)
            {
                endSeconds = Math.Min(endSeconds.Value, DurationSeconds.Value);
            }
        }

        if (endSeconds is not null && endSeconds.Value <= startSeconds)
        {
            error = "End time must be later than start time.";
            return false;
        }

        return true;
    }

    private static bool TryParseTimestampR1637(string? value, out double seconds)
    {
        seconds = 0;
        var text = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var parts = text.Split(':');
        if (parts.Length is not (2 or 3))
        {
            return false;
        }

        if (!int.TryParse(parts[^1], out var secondPart) || secondPart is < 0 or > 59 ||
            !int.TryParse(parts[^2], out var minutePart) || minutePart < 0)
        {
            return false;
        }

        var hourPart = 0;
        if (parts.Length == 3)
        {
            if (minutePart > 59 || !int.TryParse(parts[0], out hourPart) || hourPart < 0)
            {
                return false;
            }
        }

        seconds = hourPart * 3600d + minutePart * 60d + secondPart;
        return true;
    }

    private static string FormatTimestampR1637(double seconds)
    {
        var total = Math.Max(0L, (long)Math.Round(seconds, MidpointRounding.AwayFromZero));
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var secondsPart = total % 60;
        return hours > 0
            ? $"{hours:00}:{minutes:00}:{secondsPart:00}"
            : $"{total / 60:00}:{secondsPart:00}";
    }

    public static void RunClipRangeSelfTestR1637()
    {
        var item = new DownloadQueueItem(
            "Clip fixture",
            "YouTube",
            "https://www.youtube.com/watch?v=fixture",
            "1080p",
            "MP4",
            OutputFormatKind.Mp4,
            string.Empty)
        {
            DurationSeconds = 157,
            ClipStartText = "00:05"
        };

        if (!string.Equals(item.ClipEndText, "02:37", StringComparison.Ordinal) ||
            !item.TryResolveClipRangeR1637(out var start, out var end, out _) ||
            Math.Abs(start - 5) > 0.001 ||
            Math.Abs((end ?? 0) - 157) > 0.001)
        {
            throw new InvalidOperationException(
                "R1.6.37 clip range contract failed for 00:05 through 02:37.");
        }

        item.ClipEndText = "03:00";
        if (item.TryResolveClipRangeR1637(out _, out _, out _))
        {
            throw new InvalidOperationException(
                "R1.6.37 clip range contract failed: end time beyond detected duration was accepted.");
        }
    }

    // Queue selection is intentionally transient UI state. It is not persisted
    // into download-queue.json, so restarting MediaDock never auto-selects jobs.
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, value);
    }

    public string Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    public OutputFormatKind OutputKind
    {
        get => _outputKind;
        set
        {
            if (SetProperty(ref _outputKind, value))
            {
                OnPropertyChanged(nameof(CanConvertToMp3));
                OnPropertyChanged(nameof(CanRedownloadAsMp4));
            }
        }
    }

    public int Mp3BitrateKbps
    {
        get => _mp3BitrateKbps;
        set => SetProperty(ref _mp3BitrateKbps, value);
    }

    public QualityChoice? QualityChoice
    {
        get => _qualityChoice;
        set => SetProperty(ref _qualityChoice, value);
    }

    public AudioChoice? AudioChoice
    {
        get => _audioChoice;
        set => SetProperty(ref _audioChoice, value);
    }

    public MediaInfo? MediaSnapshot
    {
        get => _mediaSnapshot;
        set => SetProperty(ref _mediaSnapshot, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(CanStart));
            }
        }
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public string SpeedText
    {
        get => _speedText;
        set => SetProperty(ref _speedText, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                OnPropertyChanged(nameof(CanConvertToMp3));
                OnPropertyChanged(nameof(CanRedownloadAsMp4));
            }
        }
    }

    public bool Completed
    {
        get => _completed;
        set
        {
            if (SetProperty(ref _completed, value))
            {
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanOpenFile));
                OnPropertyChanged(nameof(CanConvertToMp3));
                OnPropertyChanged(nameof(CanRedownloadAsMp4));
            }
        }
    }

    public bool HasDownloadedMp3Counterpart
    {
        get => _hasDownloadedMp3Counterpart;
        set
        {
            if (SetProperty(ref _hasDownloadedMp3Counterpart, value))
            {
                OnPropertyChanged(nameof(CanConvertToMp3));
            }
        }
    }

    public bool HasDownloadedMp4Counterpart
    {
        get => _hasDownloadedMp4Counterpart;
        set
        {
            if (SetProperty(ref _hasDownloadedMp4Counterpart, value))
            {
                OnPropertyChanged(nameof(CanRedownloadAsMp4));
            }
        }
    }

    public bool OutputFileAvailable
    {
        get => _outputFileAvailable;
        set
        {
            if (SetProperty(ref _outputFileAvailable, value))
            {
                OnPropertyChanged(nameof(CanOpenFile));
            }
        }
    }

    public bool CanOpenFile => Completed && OutputFileAvailable;

    public bool CanStart => !Completed && string.Equals(Status, "Ready", StringComparison.OrdinalIgnoreCase);
    public bool CanConvertToMp3 => Completed && OutputKind == OutputFormatKind.Mp4 &&
        !string.IsNullOrWhiteSpace(OutputPath) && !HasDownloadedMp3Counterpart;
    public bool CanRedownloadAsMp4 => Completed && OutputKind == OutputFormatKind.Mp3 &&
        !string.IsNullOrWhiteSpace(SourceUrl) && !HasDownloadedMp4Counterpart;

    // MEDIADOCK_QUEUE_ROW_STATE_R1629
    private bool _isSelectedR1629;
    private string _selectedFormatR1629 = string.Empty;
    private string _selectedQualityR1629 = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSelectedR1629
    {
        get => _isSelectedR1629;
        set => SetProperty(ref _isSelectedR1629, value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string SelectedFormatR1629
    {
        get => string.IsNullOrWhiteSpace(_selectedFormatR1629)
            ? (OutputKind == OutputFormatKind.Mp3 ? "MP3" : "MP4")
            : _selectedFormatR1629;
        set
        {
            var normalized = string.Equals(value, "MP3", StringComparison.OrdinalIgnoreCase) ? "MP3" : "MP4";
            if (!SetProperty(ref _selectedFormatR1629, normalized))
            {
                return;
            }

            OutputKind = normalized == "MP3" ? OutputFormatKind.Mp3 : OutputFormatKind.Mp4;
            Format = normalized;

            if (OutputKind == OutputFormatKind.Mp3)
            {
                QualityChoice = null;
                SelectedQualityR1629 = "Highest";
            }
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string SelectedQualityR1629
    {
        get
        {
            if (OutputKind == OutputFormatKind.Mp3)
            {
                return "Highest";
            }

            if (!string.IsNullOrWhiteSpace(_selectedQualityR1629))
            {
                return _selectedQualityR1629;
            }

            var existing = QualityChoice?.Height;
            if (existing is 2160 or 1440 or 1080 or 720 or 480 or 360)
            {
                return existing.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "p";
            }

            foreach (var height in new[] { 2160, 1440, 1080, 720, 480, 360 })
            {
                if ((Quality ?? string.Empty).Contains(height.ToString(System.Globalization.CultureInfo.InvariantCulture) + "p", StringComparison.OrdinalIgnoreCase))
                {
                    return height.ToString(System.Globalization.CultureInfo.InvariantCulture) + "p";
                }
            }

            return "Highest";
        }
        set
        {
            var normalized = OutputKind == OutputFormatKind.Mp3
                ? "Highest"
                : value switch
                {
                    "2160p" => "2160p",
                    "1440p" => "1440p",
                    "1080p" => "1080p",
                    "720p" => "720p",
                    "480p" => "480p",
                    "360p" => "360p",
                    _ => "Highest"
                };

            if (!SetProperty(ref _selectedQualityR1629, normalized))
            {
                return;
            }

            QualityChoice = null;
            Quality = OutputKind == OutputFormatKind.Mp3
                ? "Best audio"
                : normalized == "Highest" ? "Best available" : normalized;
        }
    }
}
