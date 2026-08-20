using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Threading;
using Microsoft.Win32;
using MediaDownloader.Core.Models;
using MediaDownloader.Core.Services;
using MediaDownloader.Infrastructure;

namespace MediaDownloader.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ToolHealthService _healthService;
    private readonly YtDlpService _mediaEngine;
    private readonly FfmpegConversionService _conversionService;
    private readonly SynchronizationContext? _uiContext;
    private CancellationTokenSource? _autoAnalyzeCts;
    private CancellationTokenSource? _analysisCts;
    private int _analysisVersion;
    private long _lastDownloadProgressUiTick;

    private string _url = string.Empty;
    private string _status = string.Empty;
    private string _title = "Paste a media URL to begin";
    private string _platform = "Ready";
    private string _uploader = "Paste a public or accessible media URL above";
    private string _thumbnailUrl = string.Empty;
    private string _durationText = string.Empty;
    private string _mediaQualityText = string.Empty;
    private string _availableFormatsSummary = "Paste a URL to discover available qualities automatically.";
    private string _audioSummary = "Best available audio";
    private string _mediaCapabilityText = string.Empty;
    private string _diagnostics = string.Empty;
    private string _outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Media Downloader");
    private QualityChoice? _selectedQuality;
    private OutputFormatChoice? _selectedDownloadFormat;
    private Mp3BitrateChoice? _selectedMp3Bitrate;
    private AudioChoice? _selectedAudioChoice;
    private MediaInfo? _media;
    private PlaylistInfo? _playlist;
    private bool _busy;
    private bool _isMediaReady;
    private bool _hasAnalysisError;
    private string _analysisErrorText = string.Empty;
    private string _lastDownloadedFile = string.Empty;
    private string _conversionInputPath = string.Empty;
    private string _conversionOutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Media Downloader");
    private Mp3BitrateChoice? _selectedConversionBitrate;
    private string _lastConvertedFile = string.Empty;

    public MainWindowViewModel()
    {
        _uiContext = SynchronizationContext.Current;

        var tools = new ToolLocator();
        var runner = new ProcessRunner();
        _healthService = new ToolHealthService(tools, runner);
        _mediaEngine = new YtDlpService(tools, runner);
        _conversionService = new FfmpegConversionService(tools, runner);

        foreach (var format in new[]
        {
            new OutputFormatChoice(OutputFormatKind.Mp4, "MP4"),
            new OutputFormatChoice(OutputFormatKind.Mkv, "MKV"),
            new OutputFormatChoice(OutputFormatKind.Mp3, "MP3")
        })
        {
            DownloadFormats.Add(format);
        }

        foreach (var bitrate in new[]
        {
            new Mp3BitrateChoice(320, "320 kbps"),
            new Mp3BitrateChoice(256, "256 kbps"),
            new Mp3BitrateChoice(192, "192 kbps"),
            new Mp3BitrateChoice(128, "128 kbps")
        })
        {
            Mp3Bitrates.Add(bitrate);
            ConversionMp3Bitrates.Add(bitrate);
        }

        _selectedDownloadFormat = DownloadFormats.FirstOrDefault();
        _selectedMp3Bitrate = Mp3Bitrates.FirstOrDefault();
        _selectedConversionBitrate = ConversionMp3Bitrates.FirstOrDefault();

        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, CanAnalyze);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, CanDownload);
        RefreshHealthCommand = new AsyncRelayCommand(RefreshHealthAsync, () => !Busy);
        OpenDownloadsCommand = new AsyncRelayCommand(OpenDownloadsAsync);
        BrowseOutputDirectoryCommand = new AsyncRelayCommand(BrowseOutputDirectoryAsync, () => !Busy);
        PasteUrlCommand = new AsyncRelayCommand(PasteUrlAsync, () => !Busy);
        BrowseConversionInputCommand = new AsyncRelayCommand(BrowseConversionInputAsync, () => !Busy);
        ConvertLocalFileCommand = new AsyncRelayCommand(ConvertLocalFileAsync, CanConvertLocalFile);
        ClearCompletedCommand = new AsyncRelayCommand(ClearCompletedAsync, () => DownloadQueue.Any(item => item.Completed));

        SetOptionPlaceholders("Auto - Best available", "Best available audio");
    }

    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
                ScheduleAutoAnalyze(value);
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Platform
    {
        get => _platform;
        private set => SetProperty(ref _platform, value);
    }

    public string Uploader
    {
        get => _uploader;
        private set => SetProperty(ref _uploader, value);
    }

    public string ThumbnailUrl
    {
        get => _thumbnailUrl;
        private set => SetProperty(ref _thumbnailUrl, value);
    }

    public string DurationText
    {
        get => _durationText;
        private set => SetProperty(ref _durationText, value);
    }

    public string MediaQualityText
    {
        get => _mediaQualityText;
        private set => SetProperty(ref _mediaQualityText, value);
    }

    public string AvailableFormatsSummary
    {
        get => _availableFormatsSummary;
        private set => SetProperty(ref _availableFormatsSummary, value);
    }

    public string AudioSummary
    {
        get => _audioSummary;
        private set => SetProperty(ref _audioSummary, value);
    }

    public string MediaCapabilityText
    {
        get => _mediaCapabilityText;
        private set => SetProperty(ref _mediaCapabilityText, value);
    }

    public bool HasAnalysisError
    {
        get => _hasAnalysisError;
        private set => SetProperty(ref _hasAnalysisError, value);
    }

    public string AnalysisErrorText
    {
        get => _analysisErrorText;
        private set => SetProperty(ref _analysisErrorText, value);
    }

    public string Diagnostics
    {
        get => _diagnostics;
        private set => SetProperty(ref _diagnostics, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetProperty(ref _outputDirectory, value);
    }

    public string LastDownloadedFile
    {
        get => _lastDownloadedFile;
        private set => SetProperty(ref _lastDownloadedFile, value);
    }

    public string LastConvertedFile
    {
        get => _lastConvertedFile;
        private set => SetProperty(ref _lastConvertedFile, value);
    }

    public bool IsMediaReady
    {
        get => _isMediaReady;
        private set
        {
            if (SetProperty(ref _isMediaReady, value))
            {
                OnPropertyChanged(nameof(CanChooseQuality));
                DownloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool Busy
    {
        get => _busy;
        private set
        {
            if (SetProperty(ref _busy, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
                DownloadCommand.RaiseCanExecuteChanged();
                RefreshHealthCommand.RaiseCanExecuteChanged();
                BrowseOutputDirectoryCommand.RaiseCanExecuteChanged();
                PasteUrlCommand.RaiseCanExecuteChanged();
                BrowseConversionInputCommand.RaiseCanExecuteChanged();
                ConvertLocalFileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<ToolHealth> ToolHealth { get; } = [];
    public ObservableCollection<QualityChoice> QualityChoices { get; } = [];
    public ObservableCollection<OutputFormatChoice> DownloadFormats { get; } = [];
    public ObservableCollection<Mp3BitrateChoice> Mp3Bitrates { get; } = [];
    public ObservableCollection<AudioChoice> AudioChoices { get; } = [];
    public ObservableCollection<Mp3BitrateChoice> ConversionMp3Bitrates { get; } = [];
    public ObservableCollection<DownloadQueueItem> DownloadQueue { get; } = [];

    public QualityChoice? SelectedQuality
    {
        get => _selectedQuality;
        set
        {
            if (SetProperty(ref _selectedQuality, value))
            {
                MediaQualityText = value?.Label ?? "Quality not selected";
                DownloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public OutputFormatChoice? SelectedDownloadFormat
    {
        get => _selectedDownloadFormat;
        set
        {
            if (SetProperty(ref _selectedDownloadFormat, value))
            {
                OnPropertyChanged(nameof(IsMp3Download));
                OnPropertyChanged(nameof(IsQualitySelectionEnabled));
                OnPropertyChanged(nameof(CanChooseQuality));
                DownloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public Mp3BitrateChoice? SelectedMp3Bitrate
    {
        get => _selectedMp3Bitrate;
        set
        {
            if (SetProperty(ref _selectedMp3Bitrate, value))
            {
                DownloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AudioChoice? SelectedAudioChoice
    {
        get => _selectedAudioChoice;
        set
        {
            if (SetProperty(ref _selectedAudioChoice, value))
            {
                AudioSummary = value?.Label ?? "Best source audio";
                DownloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsMp3Download => SelectedDownloadFormat?.Kind == OutputFormatKind.Mp3;
    public bool IsQualitySelectionEnabled => !IsMp3Download;
    public bool CanChooseQuality => IsMediaReady && IsQualitySelectionEnabled;

    public string ConversionInputPath
    {
        get => _conversionInputPath;
        set
        {
            if (SetProperty(ref _conversionInputPath, value))
            {
                OnPropertyChanged(nameof(ConversionInputFileName));
                ConvertLocalFileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ConversionInputFileName => string.IsNullOrWhiteSpace(ConversionInputPath)
        ? "Drop MKV/MP4 here"
        : Path.GetFileName(ConversionInputPath);

    public string ConversionOutputDirectory
    {
        get => _conversionOutputDirectory;
        set => SetProperty(ref _conversionOutputDirectory, value);
    }

    public Mp3BitrateChoice? SelectedConversionBitrate
    {
        get => _selectedConversionBitrate;
        set
        {
            if (SetProperty(ref _selectedConversionBitrate, value))
            {
                ConvertLocalFileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand AnalyzeCommand { get; }
    public AsyncRelayCommand DownloadCommand { get; }
    public AsyncRelayCommand RefreshHealthCommand { get; }
    public AsyncRelayCommand OpenDownloadsCommand { get; }
    public AsyncRelayCommand BrowseOutputDirectoryCommand { get; }
    public AsyncRelayCommand PasteUrlCommand { get; }
    public AsyncRelayCommand BrowseConversionInputCommand { get; }
    public AsyncRelayCommand ConvertLocalFileCommand { get; }
    public AsyncRelayCommand ClearCompletedCommand { get; }

    public async Task InitializeAsync() => await RefreshHealthAsync();

    public void ReportStartupWarning(string message, Exception exception)
    {
        Status = message;
        Diagnostics = exception.ToString();
    }

    public void SetConversionInputPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var extension = Path.GetExtension(path);
        if (!new[] { ".mkv", ".mp4", ".webm", ".mov", ".m4v" }
            .Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            Status = "Choose an MKV, MP4, WebM, MOV, or M4V file.";
            return;
        }

        ConversionInputPath = path;
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ConversionOutputDirectory = folder;
        }

        Status = $"Ready to convert {Path.GetFileName(path)}";
    }

    private void SetOptionPlaceholders(string qualityLabel, string audioLabel)
    {
        QualityChoices.Clear();
        QualityChoices.Add(new QualityChoice(QualityChoiceKind.Best, null, qualityLabel));
        SelectedQuality = QualityChoices.FirstOrDefault();

        AudioChoices.Clear();
        AudioChoices.Add(new AudioChoice(audioLabel));
        SelectedAudioChoice = AudioChoices.FirstOrDefault();
    }

    private bool CanAnalyze() => !Busy;

    private bool CanDownload()
    {
        if (Busy || (_media is null && _playlist is null) || SelectedDownloadFormat is null)
        {
            return false;
        }

        if (IsMp3Download)
        {
            return SelectedMp3Bitrate is not null;
        }

        return SelectedQuality is not null;
    }

    private bool CanConvertLocalFile() =>
        !Busy &&
        !string.IsNullOrWhiteSpace(ConversionInputPath) &&
        File.Exists(ConversionInputPath) &&
        SelectedConversionBitrate is not null;

    private async Task RefreshHealthAsync()
    {
        Busy = true;
        Status = "Checking media tools...";
        try
        {
            var health = await _healthService.CheckAsync();
            ToolHealth.Clear();
            foreach (var item in health)
            {
                ToolHealth.Add(item);
            }

            var missing = health.Where(h => !h.Available).Select(h => h.Name).ToArray();
            Status = missing.Length == 0
                ? string.Empty
                : $"Missing tools: {string.Join(", ", missing)}";
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task PasteUrlAsync()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var pasted = Clipboard.GetText().Trim();
                if (string.Equals(Url.Trim(), pasted, StringComparison.Ordinal))
                {
                    ScheduleAutoAnalyze(pasted, immediate: true);
                }
                else
                {
                    Url = pasted;
                }
            }
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "Clipboard access failed";
        }

        await Task.CompletedTask;
    }

    private void ScheduleAutoAnalyze(string value, bool immediate = false)
    {
        _autoAnalyzeCts?.Cancel();
        _autoAnalyzeCts?.Dispose();
        _autoAnalyzeCts = null;

        _analysisCts?.Cancel();

        var candidate = value.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            ResetAnalysisSurface();
            return;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Scheme is not "http" and not "https")
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _autoAnalyzeCts = cts;
        _ = AutoAnalyzeAfterDelayAsync(candidate, immediate ? 75 : 550, cts.Token);
    }

    private async Task AutoAnalyzeAfterDelayAsync(string candidate, int delayMilliseconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delayMilliseconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(Url.Trim(), candidate, StringComparison.Ordinal))
            {
                return;
            }

            await AnalyzeUrlAsync(candidate, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer URL replaced this pending/active analysis.
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "Automatic analysis failed. Open Diagnostics for details.";
        }
    }

    private async Task AnalyzeAsync()
    {
        await AnalyzeUrlAsync(Url.Trim(), CancellationToken.None);
    }

    private async Task AnalyzeUrlAsync(string rawUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) || uri.Scheme is not "http" and not "https")
        {
            return;
        }

        var version = Interlocked.Increment(ref _analysisVersion);
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _analysisCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _analysisCts.Token;

        Busy = true;
        Status = "Analyzing automatically...";
        Diagnostics = string.Empty;
        HasAnalysisError = false;
        AnalysisErrorText = string.Empty;
        Title = "Analyzing media...";
        Platform = "Detecting source";
        Uploader = string.Empty;
        ThumbnailUrl = string.Empty;
        DurationText = string.Empty;
        MediaQualityText = "Analyzing";
        AvailableFormatsSummary = "Discovering available video sizes and audio...";
        AudioSummary = "Analyzing audio";
        MediaCapabilityText = string.Empty;
        IsMediaReady = false;
        _media = null;
        _playlist = null;
        SetOptionPlaceholders("Analyzing quality...", "Analyzing audio...");

        try
        {
            var normalizedUrl = YtDlpService.NormalizeUserUrl(rawUrl);
            var result = await _mediaEngine.AnalyzeAsync(normalizedUrl, token);
            token.ThrowIfCancellationRequested();

            if (version != _analysisVersion || !string.Equals(Url.Trim(), rawUrl, StringComparison.Ordinal))
            {
                return;
            }

            Diagnostics = result.Diagnostics;

            if (result.IsPlaylist && result.Playlist is not null)
            {
                _playlist = result.Playlist;
                Title = result.Playlist.Title;
                Platform = FriendlyExtractorName(result.Playlist.Extractor);
                Uploader = string.IsNullOrWhiteSpace(result.Playlist.Uploader) ? "Playlist" : result.Playlist.Uploader;
                ThumbnailUrl = result.Playlist.ThumbnailUrl;
                DurationText = $"{result.Playlist.Entries.Count} videos";
                MediaQualityText = "Best per video";

                QualityChoices.Clear();
                SelectedQuality = null;
                foreach (var choice in YtDlpService.BuildPlaylistQualityChoices())
                {
                    QualityChoices.Add(choice);
                }

                SelectedQuality = QualityChoices.FirstOrDefault();
                AudioChoices.Clear();
                SelectedAudioChoice = null;
                AudioChoices.Add(new AudioChoice("Best available audio per video"));
                SelectedAudioChoice = AudioChoices.FirstOrDefault();
                AvailableFormatsSummary = $"Explicit playlist · {result.Playlist.Entries.Count} accessible item(s)";
                AudioSummary = "Best available audio per video";
                MediaCapabilityText = $"Playlist · {result.Playlist.Entries.Count}";
                IsMediaReady = true;
                Status = $"Playlist ready: {result.Playlist.Entries.Count} item(s)";
                return;
            }

            if (result.Media is null)
            {
                throw new MediaEngineException("Analysis returned no media information.", result.Diagnostics);
            }

            _media = result.Media;
            Title = result.Media.Title;
            Platform = FriendlyExtractorName(result.Media.Extractor);
            Uploader = string.IsNullOrWhiteSpace(result.Media.Uploader) ? "Unknown creator" : result.Media.Uploader;
            ThumbnailUrl = result.Media.ThumbnailUrl;
            DurationText = FormatDuration(result.Media.DurationSeconds);

            QualityChoices.Clear();
            SelectedQuality = null;
            foreach (var choice in YtDlpService.BuildQualityChoices(result.Media))
            {
                QualityChoices.Add(choice);
            }

            SelectedQuality = YtDlpService.SelectPreferredDefaultQuality(QualityChoices);

            var qualityLabels = QualityChoices
                .Where(choice => choice.Kind == QualityChoiceKind.ExactHeight)
                .Take(6)
                .Select(choice => choice.Fps is > 0
                    ? $"{choice.Height}p{choice.Fps}"
                    : $"{choice.Height}p")
                .ToArray();

            AvailableFormatsSummary = qualityLabels.Length == 0
                ? "Audio-only media available"
                : $"Available: {string.Join(" · ", qualityLabels)}";

            AudioChoices.Clear();
            SelectedAudioChoice = null;
            foreach (var audioChoice in YtDlpService.BuildAudioChoices(result.Media))
            {
                AudioChoices.Add(audioChoice);
            }
            SelectedAudioChoice = YtDlpService.SelectPreferredDefaultAudio(AudioChoices);

            MediaCapabilityText = result.Media.Formats.Any(format => format.HasVideo && !format.IsDrm)
                ? "Video + Audio"
                : "Audio Only";

            IsMediaReady = true;
            Status = "Ready to download";
        }
        catch (OperationCanceledException)
        {
            // Expected when the user pastes/types another URL during analysis.
        }
        catch (MediaEngineException ex)
        {
            if (version != _analysisVersion)
            {
                return;
            }

            Diagnostics = ex.Diagnostics;
            Status = "Analysis failed. Open Diagnostics for details.";
            Title = "Could not analyze this URL";
            Platform = "Analysis error";
            Uploader = string.Empty;
            AvailableFormatsSummary = "No usable formats were detected";
            HasAnalysisError = true;
            AnalysisErrorText = "The source could not be analyzed. View technical details in Diagnostics.";
            SetOptionPlaceholders("Auto - Best available", "Best available audio");
        }
        catch (Exception ex)
        {
            if (version != _analysisVersion)
            {
                return;
            }

            Diagnostics = ex.ToString();
            Status = "Analysis failed. Open Diagnostics for details.";
            Title = "Analysis did not complete";
            Platform = "Analysis error";
            Uploader = string.Empty;
            AvailableFormatsSummary = "No formats available";
            HasAnalysisError = true;
            AnalysisErrorText = "Media Downloader encountered unexpected metadata. View technical details in Diagnostics.";
            SetOptionPlaceholders("Auto - Best available", "Best available audio");
        }
        finally
        {
            if (version == _analysisVersion)
            {
                Busy = false;
                _analysisCts?.Dispose();
                _analysisCts = null;
            }
        }
    }

    private void ResetAnalysisSurface()
    {
        Interlocked.Increment(ref _analysisVersion);
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _analysisCts = null;
        Busy = false;
        _media = null;
        _playlist = null;
        SetOptionPlaceholders("Auto - Best available", "Best available audio");
        IsMediaReady = false;
        HasAnalysisError = false;
        AnalysisErrorText = string.Empty;
        Title = "Paste a media URL to begin";
        Platform = "Ready";
        Uploader = "Paste a public or accessible media URL above";
        ThumbnailUrl = string.Empty;
        DurationText = string.Empty;
        MediaQualityText = "Auto - Best available";
        AvailableFormatsSummary = "Paste a URL to discover available qualities automatically.";
        AudioSummary = "Best available audio";
        MediaCapabilityText = string.Empty;
        Status = string.Empty;
    }

    private bool ShouldPublishDownloadProgress(string line)
    {
        if (!line.StartsWith("MDPROGRESS|", StringComparison.Ordinal))
        {
            return false;
        }

        if (line.StartsWith("MDPROGRESS|finished|", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Exchange(ref _lastDownloadProgressUiTick, Environment.TickCount64);
            return true;
        }

        var now = Environment.TickCount64;
        while (true)
        {
            var previous = Interlocked.Read(ref _lastDownloadProgressUiTick);
            if (now - previous < 200)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _lastDownloadProgressUiTick, now, previous) == previous)
            {
                return true;
            }
        }
    }

    private async Task DownloadAsync()
    {
        if ((_media is null && _playlist is null) || SelectedDownloadFormat is null)
        {
            return;
        }

        DownloadQueueItem? queueItem = null;
        try
        {
            Busy = true;
            Interlocked.Exchange(ref _lastDownloadProgressUiTick, 0);
            LastDownloadedFile = string.Empty;
            Diagnostics = string.Empty;

            var title = _playlist?.Title ?? _media?.Title ?? "Media";
            var thumbnail = _playlist?.ThumbnailUrl ?? _media?.ThumbnailUrl ?? string.Empty;
            var qualityLabel = _playlist is not null
                ? "Best per video"
                : IsMp3Download ? "Best audio" : SelectedQuality?.Label ?? "Best available";

            Status = _playlist is not null
                ? $"Downloading playlist: {title}"
                : $"Downloading {title}...";

            queueItem = new DownloadQueueItem(
                title,
                Platform,
                qualityLabel,
                SelectedDownloadFormat.Label,
                thumbnail)
            {
                Status = _playlist is not null ? "Preparing playlist" : "Starting",
                ProgressText = _playlist is not null ? $"{_playlist.Entries.Count} items" : "Preparing download"
            };

            DownloadQueue.Insert(0, queueItem);
            var activeQueueItem = queueItem;
            ClearCompletedCommand.RaiseCanExecuteChanged();

            WriteDownloadAttemptLog();

            const int maxProgressDiagnosticLines = 4000;
            var progressLines = new List<string>();
            void AddProgressDiagnostic(string line)
            {
                if (progressLines.Count >= maxProgressDiagnosticLines)
                {
                    progressLines.RemoveAt(0);
                }
                progressLines.Add(line);
            }

            string path;
            if (_playlist is not null)
            {
                path = await _mediaEngine.DownloadPlaylistAsync(
                    _playlist,
                    SelectedDownloadFormat.Kind,
                    SelectedMp3Bitrate?.KilobitsPerSecond ?? 320,
                    OutputDirectory,
                    onProgress: line =>
                    {
                        AddProgressDiagnostic(line);
                        if (ShouldPublishDownloadProgress(line))
                        {
                            PostToUi(() => ApplyQueueProgress(activeQueueItem, line));
                        }
                    });
            }
            else
            {
                path = await _mediaEngine.DownloadAsync(
                    _media!,
                    SelectedQuality,
                    SelectedAudioChoice,
                    SelectedDownloadFormat.Kind,
                    SelectedMp3Bitrate?.KilobitsPerSecond ?? 320,
                    OutputDirectory,
                    onProgress: line =>
                    {
                        AddProgressDiagnostic(line);
                        if (ShouldPublishDownloadProgress(line))
                        {
                            PostToUi(() => ApplyQueueProgress(activeQueueItem, line));
                        }
                    });
            }

            Diagnostics = string.Join(Environment.NewLine, progressLines);
            LastDownloadedFile = path;

            queueItem.ProgressPercent = 100;
            queueItem.Status = "Completed";
            queueItem.Completed = true;
            queueItem.OutputPath = path;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                queueItem.ProgressText = FormatBytes(FileInfoLength(path));
            }
            else if (_playlist is not null)
            {
                queueItem.ProgressText = $"{_playlist.Entries.Count} playlist item(s) processed";
            }
            queueItem.SpeedText = string.Empty;

            Status = _playlist is not null
                ? "Playlist download completed"
                : string.IsNullOrWhiteSpace(path)
                    ? "Download completed"
                    : $"Completed: {Path.GetFileName(path)}";
        }
        catch (MediaEngineException ex)
        {
            Diagnostics = ex.Diagnostics;
            if (queueItem is not null)
            {
                queueItem.Status = "Failed";
                queueItem.ProgressText = "Open Diagnostics for details";
            }
            Status = "Download failed. Open Diagnostics for details.";
            App.WriteCrashLog("DownloadEngineFailure", ex);
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            if (queueItem is not null)
            {
                queueItem.Status = "Failed";
                queueItem.ProgressText = "Open Diagnostics for details";
            }
            Status = "Download failed. Open Diagnostics for details.";
            App.WriteCrashLog("DownloadCommandFailure", ex);
        }
        finally
        {
            Busy = false;
            ClearCompletedCommand.RaiseCanExecuteChanged();
        }
    }

    private void WriteDownloadAttemptLog()
    {
        try
        {
            var directory = App.GetCrashLogDirectory();
            var lines = new[]
            {
                "Media Downloader last download attempt",
                $"Timestamp: {DateTimeOffset.Now:O}",
                $"ProcessId: {Environment.ProcessId}",
                $"URL: {_playlist?.WebpageUrl ?? _media?.WebpageUrl ?? string.Empty}",
                $"OutputFormat: {SelectedDownloadFormat?.Label ?? string.Empty}",
                $"Quality: {SelectedQuality?.Label ?? string.Empty}",
                $"QualityFormatId: {SelectedQuality?.FormatId ?? string.Empty}",
                $"Audio: {SelectedAudioChoice?.Label ?? string.Empty}",
                $"AudioFormatId: {SelectedAudioChoice?.FormatId ?? string.Empty}",
                $"Mp3Bitrate: {SelectedMp3Bitrate?.KilobitsPerSecond ?? 0}",
                $"OutputDirectory: {OutputDirectory}"
            };
            File.WriteAllLines(Path.Combine(directory, "Last-Download-Attempt.txt"), lines);
        }
        catch
        {
            // Diagnostics must never interfere with the download path.
        }
    }

    private async Task BrowseOutputDirectoryAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose Media Downloader save location",
            InitialDirectory = Directory.Exists(OutputDirectory) ? OutputDirectory : string.Empty
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            OutputDirectory = dialog.FolderName;
            Status = "Save location updated";
        }

        await Task.CompletedTask;
    }

    private async Task BrowseConversionInputAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a video to convert to MP3",
            Filter = "Video files (*.mkv;*.mp4;*.webm;*.mov;*.m4v)|*.mkv;*.mp4;*.webm;*.mov;*.m4v|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            SetConversionInputPath(dialog.FileName);
        }

        await Task.CompletedTask;
    }

    private async Task ConvertLocalFileAsync()
    {
        Busy = true;
        Diagnostics = string.Empty;
        LastConvertedFile = string.Empty;
        Status = "Converting local file to MP3...";

        try
        {
            var log = new List<string>();
            var output = await _conversionService.ConvertToMp3Async(
                ConversionInputPath,
                ConversionOutputDirectory,
                SelectedConversionBitrate?.KilobitsPerSecond ?? 320,
                onOutput: line => log.Add(line),
                onError: line => log.Add(line));

            Diagnostics = string.Join(Environment.NewLine, log);
            LastConvertedFile = output;
            Status = $"Converted: {Path.GetFileName(output)}";
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task ClearCompletedAsync()
    {
        var completed = DownloadQueue.Where(item => item.Completed).ToArray();
        foreach (var item in completed)
        {
            DownloadQueue.Remove(item);
        }
        ClearCompletedCommand.RaiseCanExecuteChanged();
        await Task.CompletedTask;
    }

    private Task OpenDownloadsAsync()
    {
        Directory.CreateDirectory(OutputDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = OutputDirectory,
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    private void ApplyQueueProgress(DownloadQueueItem item, string line)
    {
        var parts = line.Split('|');
        if (parts.Length < 6)
        {
            item.Status = "Downloading";
            return;
        }

        var state = parts[1];
        var downloadedBytes = ParseDouble(parts[2]);
        var totalBytes = ParseDouble(parts[3]);
        var speed = ParseDouble(parts[4]);
        var eta = parts[5];
        var itemPercent = totalBytes > 0 && downloadedBytes >= 0
            ? Math.Clamp(downloadedBytes / totalBytes * 100.0, 0, 100)
            : 0;

        var playlistIndex = parts.Length > 6 ? ParseInt(parts[6]) : 0;
        var playlistCount = parts.Length > 7 ? ParseInt(parts[7]) : 0;
        var playlistTitle = parts.Length > 8 ? parts[8].Trim() : string.Empty;

        if (playlistIndex > 0 && playlistCount > 0)
        {
            item.ProgressPercent = Math.Clamp(((playlistIndex - 1) + itemPercent / 100.0) / playlistCount * 100.0, 0, 100);
            item.ProgressText = $"Item {playlistIndex}/{playlistCount}";
            if (!string.IsNullOrWhiteSpace(playlistTitle) && playlistTitle != "NA")
            {
                item.Status = Truncate($"{playlistIndex}/{playlistCount} · {playlistTitle}", 78);
            }
            else
            {
                item.Status = $"Downloading item {playlistIndex}/{playlistCount}";
            }
        }
        else
        {
            item.ProgressPercent = itemPercent;
            if (totalBytes > 0 && downloadedBytes >= 0)
            {
                item.ProgressText = $"{FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)}";
            }
            else if (downloadedBytes > 0)
            {
                item.ProgressText = FormatBytes(downloadedBytes);
            }

            item.Status = string.Equals(state, "finished", StringComparison.OrdinalIgnoreCase)
                ? "Processing"
                : string.IsNullOrWhiteSpace(eta) || eta == "NA"
                    ? "Downloading"
                    : $"Downloading · ETA {eta}s";
        }

        item.SpeedText = speed > 0 ? $"{FormatBytes(speed)}/s" : string.Empty;
        Status = item.Status;
    }

    private void PostToUi(Action action)
    {
        void SafeAction()
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                App.WriteCrashLog("DeferredUiProgress", ex);
            }
        }

        if (_uiContext is null)
        {
            SafeAction();
            return;
        }

        _uiContext.Post(_ => SafeAction(), null);
    }

    private static string FriendlyExtractorName(string extractor)
    {
        if (string.IsNullOrWhiteSpace(extractor))
        {
            return "Media source";
        }

        if (extractor.Contains("youtube", StringComparison.OrdinalIgnoreCase)) return "YouTube";
        if (extractor.Contains("vimeo", StringComparison.OrdinalIgnoreCase)) return "Vimeo";
        if (extractor.Contains("instagram", StringComparison.OrdinalIgnoreCase)) return "Instagram";
        if (extractor.Contains("tiktok", StringComparison.OrdinalIgnoreCase)) return "TikTok";
        if (extractor.Contains("twitter", StringComparison.OrdinalIgnoreCase) || extractor.Equals("X", StringComparison.OrdinalIgnoreCase)) return "X / Twitter";
        if (extractor.Contains("facebook", StringComparison.OrdinalIgnoreCase)) return "Facebook";

        return extractor;
    }

    private static string FormatDuration(double? seconds)
    {
        if (seconds is null || seconds <= 0)
        {
            return "--:--";
        }

        var span = TimeSpan.FromSeconds(seconds.Value);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes}:{span.Seconds:00}";
    }

    private static string BuildAudioSummary(MediaFormat format)
    {
        var codec = string.IsNullOrWhiteSpace(format.AudioCodec) || format.AudioCodec == "none"
            ? "Audio"
            : format.AudioCodec.ToUpperInvariant();
        var bitrate = format.AudioBitrate is > 0 ? $" · {Math.Round(format.AudioBitrate.Value):0} kbps" : string.Empty;
        return $"Best source · {codec}{bitrate}";
    }

    private static double ParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(1, maxLength - 1)] + "…";
    }

    private static long FileInfoLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatBytes(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var bytes)
            ? FormatBytes(bytes)
            : string.Empty;
    }

    private static string FormatBytes(double bytes)
    {
        if (bytes <= 0)
        {
            return string.Empty;
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var index = 0;
        while (bytes >= 1024 && index < units.Length - 1)
        {
            bytes /= 1024;
            index++;
        }

        return $"{bytes:0.##} {units[index]}";
    }
}
