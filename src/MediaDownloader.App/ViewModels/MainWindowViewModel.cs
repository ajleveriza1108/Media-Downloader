using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
    // MEDIADOCK_BUILD_IDENTITY_BINDING_R1623
    public string BuildIdentityText =>
        BuildIdentity.DisplayLabel;
    private readonly ToolHealthService _healthService;
    private readonly YtDlpService _mediaEngine;
    private readonly FfmpegConversionService _conversionService;
    private readonly AppSettingsService _settingsService;
    private readonly QueuePersistenceService _queuePersistenceService;
    private readonly TrialStateService _trialStateService;
    private readonly QueueDownloadPreferencesService _queueDownloadPreferencesService;
    private readonly QueueDownloadPreferences _queueDownloadPreferences;
    private TrialStateSnapshot _trialState;
    private readonly AppSettings _settings;
    private readonly bool _queuePersistenceEnabled;
    private bool _restoringQueue;
    private readonly SynchronizationContext? _uiContext;
    private CancellationTokenSource? _autoAnalyzeCts;
    private CancellationTokenSource? _analysisCts;
    private CancellationTokenSource? _streamAutoResolveCts;
    private CancellationTokenSource? _streamResolveCts;
    private int _analysisVersion;
    private int _streamResolveVersion;
    private long _lastDownloadProgressUiTick;
    private bool _batchUpdatingPlaylistSelection;
    private int _playlistPageIndex;
    private const int PlaylistPageSize = 4;
    private int _queuePageIndex;
    private const int QueuePageSize = 4;
    private DownloadQueueItem? _activeQueueItem;
    private bool _bulkQueueDownloadActive;
    private const int MaxConcurrentQueueDownloadsR1630 = 5;
    private readonly object _trialAccountingGateR1630 = new();

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
    private string _outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "MediaDock");
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
    private string _conversionOutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "MediaDock");
    private Mp3BitrateChoice? _selectedConversionBitrate;
    private string _lastConvertedFile = string.Empty;

    private string _streamUrl = string.Empty;
    private string _streamResolvedUrl = string.Empty;
    private MediaInfo? _streamMedia;
    private QualityChoice? _selectedStreamQuality;
    private string _streamTitle = "Paste a media link to detect video";
    private string _streamPlatform = "Ready";
    private string _streamUploader = "MediaDock will resolve the playable media inside the link.";
    private string _streamThumbnailUrl = string.Empty;
    private string _streamDurationText = string.Empty;
    private string _streamKindText = "Waiting for a link";
    private string _streamStatus = "Paste a public or accessible media link.";
    private string _streamTechnicalText = "Direct media playback avoids loading the source webpage and its page-level ads.";
    private bool _isStreamReady;

    public MainWindowViewModel(bool queuePersistenceEnabled = true)
    {
        _queuePersistenceEnabled = queuePersistenceEnabled;
        _uiContext = SynchronizationContext.Current;
        _settingsService = new AppSettingsService();
        _queuePersistenceService = new QueuePersistenceService();
        _trialStateService = new TrialStateService();
        _trialState = _trialStateService.Load();
        _settings = _settingsService.Load();
        _queueDownloadPreferencesService = new QueueDownloadPreferencesService();
        _queueDownloadPreferences = _queueDownloadPreferencesService.Load();

        _outputDirectory = _settings.OutputDirectory;
        _conversionOutputDirectory = _settings.ConversionOutputDirectory;

        var tools = new ToolLocator();
        var runner = new ProcessRunner();
        _healthService = new ToolHealthService(tools, runner);
        _mediaEngine = new YtDlpService(tools, runner);
        _conversionService = new FfmpegConversionService(tools, runner);

        foreach (var format in new[]
        {
            new OutputFormatChoice(OutputFormatKind.Mp4, "MP4"),
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
        _selectedMp3Bitrate = Mp3Bitrates.FirstOrDefault(
            bitrate => bitrate.KilobitsPerSecond == _settings.DownloadMp3BitrateKbps) ?? Mp3Bitrates.FirstOrDefault();
        _selectedConversionBitrate = ConversionMp3Bitrates.FirstOrDefault(
            bitrate => bitrate.KilobitsPerSecond == _settings.ConversionMp3BitrateKbps) ?? ConversionMp3Bitrates.FirstOrDefault();

        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, CanAnalyze);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, CanDownload);
        RefreshHealthCommand = new AsyncRelayCommand(RefreshHealthAsync, () => !Busy);
        OpenDownloadsCommand = new AsyncRelayCommand(OpenDownloadsAsync);
        BrowseOutputDirectoryCommand = new AsyncRelayCommand(BrowseOutputDirectoryAsync, () => !Busy);
        PasteUrlCommand = new AsyncRelayCommand(PasteUrlAsync, () => !Busy && !IsTrialExhausted);
        ImportLinksCommand = new AsyncRelayCommand(ImportLinkFilesFromDialogAsync, () => !Busy && !IsTrialExhausted);
        PasteStreamUrlCommand = new AsyncRelayCommand(PasteStreamUrlAsync, () => !Busy && PremiumWorkspacesEnabled);
        ResolveStreamCommand = new AsyncRelayCommand(ResolveStreamAsync, CanResolveStream);
        BrowseConversionInputCommand = new AsyncRelayCommand(BrowseConversionInputAsync, () => !Busy && PremiumWorkspacesEnabled);
        ConvertLocalFileCommand = new AsyncRelayCommand(ConvertLocalFileAsync, CanConvertLocalFile);
        ClearCompletedCommand = new AsyncRelayCommand(ClearCompletedAsync, () => DownloadQueue.Any(item => item.Completed));
        SelectAllQueueCommand = new AsyncRelayCommand(SelectAllQueueAsync, CanSelectAllQueue);
        ClearQueueSelectionCommand = new AsyncRelayCommand(ClearQueueSelectionAsync, CanClearQueueSelection);
        DownloadAllQueueCommand = new AsyncRelayCommand(DownloadAllQueueAsync, CanDownloadAllQueue);
        DownloadSelectedQueueCommand = new AsyncRelayCommand(DownloadSelectedQueueAsync, CanDownloadSelectedQueue);
        RemoveSelectedQueueCommand = new AsyncRelayCommand(RemoveSelectedQueueAsync, CanRemoveSelectedQueue);
        SelectAllPlaylistCommand = new AsyncRelayCommand(SelectAllPlaylistAsync, CanSelectAllPlaylist);
        ClearPlaylistSelectionCommand = new AsyncRelayCommand(ClearPlaylistSelectionAsync, CanClearPlaylistSelection);
        PreviousPlaylistPageCommand = new AsyncRelayCommand(PreviousPlaylistPageAsync, () => CanGoPreviousPlaylistPage);
        NextPlaylistPageCommand = new AsyncRelayCommand(NextPlaylistPageAsync, () => CanGoNextPlaylistPage);
        PreviousQueuePageCommand = new AsyncRelayCommand(PreviousQueuePageAsync, () => CanGoPreviousQueuePage);
        NextQueuePageCommand = new AsyncRelayCommand(NextQueuePageAsync, () => CanGoNextQueuePage);
        DownloadQueue.CollectionChanged += DownloadQueue_CollectionChanged;
        if (_queuePersistenceEnabled)
        {
            RestorePersistedQueue();
        }

        RefreshQueuePage();

        // MEDIADOCK_SUBTITLE_COMMAND_R1628
        DownloadSubtitlesCommand =
            new AsyncRelayCommand(
                DownloadSubtitlesR1628Async,
                CanDownloadSubtitlesR1628);
        SetOptionPlaceholders("Auto - Best available", "Best available audio");

        StreamQualityChoices.Add(new QualityChoice(
            QualityChoiceKind.Best,
            null,
            "Auto - Internal media detector"));
        SelectedStreamQuality = StreamQualityChoices[0];

        if (IsTrialMode && _trialState.TamperDetected)
        {
            Status = "Trial security validation failed. Trial output is locked until MediaDock licensing is activated.";
        }
        else if (IsTrialExhausted)
        {
            Status = "Trial complete. A MediaDock license is required.";
        }
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

    public string StreamUrl
    {
        get => _streamUrl;
        set
        {
            if (SetProperty(ref _streamUrl, value))
            {
                ResolveStreamCommand.RaiseCanExecuteChanged();
                ScheduleAutoStreamResolve(value);
            }
        }
    }

    public string StreamResolvedUrl
    {
        get => _streamResolvedUrl;
        private set => SetProperty(ref _streamResolvedUrl, value);
    }

    public ObservableCollection<QualityChoice> StreamQualityChoices { get; } = [];

    public QualityChoice? SelectedStreamQuality
    {
        get => _selectedStreamQuality;
        set
        {
            if (SetProperty(ref _selectedStreamQuality, value))
            {
                StreamTechnicalText = value?.Kind == QualityChoiceKind.ExactHeight
                    ? $"Selected direct stream: {value.Label}"
                    : "Auto uses MediaDock's internal browser/network detector and the webpage's own video engine.";
            }
        }
    }

    public string StreamTitle
    {
        get => _streamTitle;
        private set => SetProperty(ref _streamTitle, value);
    }

    public string StreamPlatform
    {
        get => _streamPlatform;
        private set => SetProperty(ref _streamPlatform, value);
    }

    public string StreamUploader
    {
        get => _streamUploader;
        private set => SetProperty(ref _streamUploader, value);
    }

    public string StreamThumbnailUrl
    {
        get => _streamThumbnailUrl;
        private set => SetProperty(ref _streamThumbnailUrl, value);
    }

    public string StreamDurationText
    {
        get => _streamDurationText;
        private set => SetProperty(ref _streamDurationText, value);
    }

    public string StreamKindText
    {
        get => _streamKindText;
        private set => SetProperty(ref _streamKindText, value);
    }

    public string StreamStatus
    {
        get => _streamStatus;
        private set => SetProperty(ref _streamStatus, value);
    }

    public string StreamTechnicalText
    {
        get => _streamTechnicalText;
        private set => SetProperty(ref _streamTechnicalText, value);
    }

    public bool IsStreamReady
    {
        get => _isStreamReady;
        private set => SetProperty(ref _isStreamReady, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, MediaDownloader.Core.Services.UiTextSanitizer.NormalizeLabel(value));
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
        set
        {
            if (SetProperty(ref _outputDirectory, value))
            {
                _settings.OutputDirectory = value;
                SaveSettingsSafely();
            }
        }
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
                SyncActiveQueueSelection();
                OnPropertyChanged(nameof(DownloadButtonText));
                DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
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
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
                RefreshHealthCommand.RaiseCanExecuteChanged();
                BrowseOutputDirectoryCommand.RaiseCanExecuteChanged();
                PasteUrlCommand.RaiseCanExecuteChanged();
                ImportLinksCommand.RaiseCanExecuteChanged();
                PasteStreamUrlCommand.RaiseCanExecuteChanged();
                ResolveStreamCommand.RaiseCanExecuteChanged();
                BrowseConversionInputCommand.RaiseCanExecuteChanged();
                ConvertLocalFileCommand.RaiseCanExecuteChanged();
                SelectAllPlaylistCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(TrialSummary));
                ClearPlaylistSelectionCommand.RaiseCanExecuteChanged();
                RaiseQueueBulkCanExecuteChanged();
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
    public ObservableCollection<DownloadQueueItem> QueuePageEntries { get; } = [];
    public ObservableCollection<PlaylistSelectionItem> PlaylistEntries { get; } = [];
    public ObservableCollection<PlaylistSelectionItem> PlaylistPageEntries { get; } = [];

    public QualityChoice? SelectedQuality
    {
        get => _selectedQuality;
        set
        {
            if (SetProperty(ref _selectedQuality, value))
            {
                MediaQualityText = value?.Label ?? "Quality not selected";
                SyncActiveQueueSelection();
                DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
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
                SyncActiveQueueSelection();
                OnPropertyChanged(nameof(DownloadButtonText));
                DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
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
                _settings.DownloadMp3BitrateKbps = value?.KilobitsPerSecond ?? 320;
                SaveSettingsSafely();
                SyncActiveQueueSelection();
                DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
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
                SyncActiveQueueSelection();
                DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsMp3Download => SelectedDownloadFormat?.Kind == OutputFormatKind.Mp3;
    public bool IsQualitySelectionEnabled => !IsMp3Download;
    public bool CanChooseQuality => IsMediaReady && IsQualitySelectionEnabled;
    public bool IsPlaylistReady => _playlist is not null && PlaylistEntries.Count > 0;
    public int PlaylistSelectedCount => PlaylistEntries.Count(item => item.IsSelected);
    public int PlaylistTotalCount => PlaylistEntries.Count;
    public string PlaylistSelectionSummary => IsPlaylistReady
        ? $"{PlaylistSelectedCount} of {PlaylistTotalCount} selected"
        : string.Empty;
    public int PlaylistPageCount => Math.Max(1, (int)Math.Ceiling(PlaylistTotalCount / (double)PlaylistPageSize));
    public int PlaylistPageNumber => IsPlaylistReady ? _playlistPageIndex + 1 : 0;
    public string PlaylistPageSummary => IsPlaylistReady ? $"Page {PlaylistPageNumber} of {PlaylistPageCount}" : string.Empty;
    public bool CanGoPreviousPlaylistPage => IsPlaylistReady && _playlistPageIndex > 0;
    public bool CanGoNextPlaylistPage => IsPlaylistReady && _playlistPageIndex + 1 < PlaylistPageCount;

    // Queue pagination was removed in R1.6.17. QueuePageEntries remains as a
    // compatibility mirror for startup smoke tests, but always contains the
    // entire queue and the UI scrolls only when the visible panel is full.
    public int QueuePageCount => DownloadQueue.Count == 0 ? 0 : 1;
    public int QueuePageNumber => DownloadQueue.Count == 0 ? 0 : 1;
    public string QueuePageSummary => DownloadQueue.Count == 0
        ? "Queue empty"
        : $"{DownloadQueue.Count} item(s)";
    public bool CanGoPreviousQueuePage => false;
    public bool CanGoNextQueuePage => false;

    // R1.6.30: all trial/premium behavior follows the shared license entitlement.
    // MEDIADOCK_ENTITLEMENT_QUEUE_CONSISTENCY_R1630
    public bool IsTrialMode => !LicenseEntitlementState.IsLicensed;
    public bool PremiumWorkspacesEnabled => LicenseEntitlementState.IsLicensed;
    public int QueueSelectedCount => DownloadQueue.Count(item => item.IsSelected);
    public string QueueSelectionSummary => QueueSelectedCount == 0
        ? "No items selected"
        : $"{QueueSelectedCount} selected";

    public int TrialVideoUsed => _trialState.VideoCompleted;
    public int TrialMp3Used => _trialState.Mp3Completed;
    public int TrialVideoRemaining => Math.Max(0, TrialStateService.MaxVideoOutputs - TrialVideoUsed);
    public int TrialMp3Remaining => Math.Max(0, TrialStateService.MaxMp3Outputs - TrialMp3Used);
    public bool IsVideoTrialExhausted => IsTrialMode && (TrialVideoRemaining == 0 || _trialState.TamperDetected);
    public bool IsMp3TrialExhausted => IsTrialMode && (TrialMp3Remaining == 0 || _trialState.TamperDetected);
    public bool IsTrialExhausted => IsTrialMode && IsVideoTrialExhausted && IsMp3TrialExhausted;
    public bool TrialTamperDetected => IsTrialMode && _trialState.TamperDetected;
    public string TrialSummary => !IsTrialMode
        ? "Licensed: MediaDock"
        : TrialTamperDetected
            ? "Trial locked"
            : $"Trial: Video {TrialVideoUsed}/{TrialStateService.MaxVideoOutputs} | MP3 {TrialMp3Used}/{TrialStateService.MaxMp3Outputs}";
    public string TrialLockTitle => TrialTamperDetected ? "Trial security validation failed" : "MediaDock trial complete";
    public string TrialLockMessage => TrialTamperDetected
        ? "MediaDock detected an invalid protected trial state. Trial download and conversion output is locked. Reinstalling or deleting local settings does not restore the trial."
        : $"You have completed {TrialStateService.MaxVideoOutputs} video outputs and {TrialStateService.MaxMp3Outputs} MP3 outputs. A MediaDock license is required for more downloads. Stream and Convert are full-version features.";

    public string DownloadButtonText => _playlist is not null
        ? PlaylistSelectedCount == 0
            ? "Select videos"
            : PlaylistSelectedCount == PlaylistTotalCount
                ? $"Add All to Queue ({PlaylistTotalCount})"
                : $"Add Selected to Queue ({PlaylistSelectedCount})"
        : _activeQueueItem?.CanStart == true
            ? "Download Ready"
            : "Download";

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
        ? "Drop video or audio here"
        : Path.GetFileName(ConversionInputPath);

    public string ConversionOutputDirectory
    {
        get => _conversionOutputDirectory;
        set
        {
            if (SetProperty(ref _conversionOutputDirectory, value))
            {
                _settings.ConversionOutputDirectory = value;
                SaveSettingsSafely();
            }
        }
    }

    public Mp3BitrateChoice? SelectedConversionBitrate
    {
        get => _selectedConversionBitrate;
        set
        {
            if (SetProperty(ref _selectedConversionBitrate, value))
            {
                _settings.ConversionMp3BitrateKbps = value?.KilobitsPerSecond ?? 320;
                SaveSettingsSafely();
                ConvertLocalFileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool AudioFadeInOut3Seconds
    {
        get => _settings.AudioFadeInOut3Seconds;
        set
        {
            if (_settings.AudioFadeInOut3Seconds == value)
            {
                return;
            }

            _settings.AudioFadeInOut3Seconds = value;
            OnPropertyChanged();
            SaveSettingsSafely();
        }
    }

    public bool AutoRemoveTikTokWatermark
    {
        get => _settings.AutoRemoveTikTokWatermark;
        set
        {
            if (_settings.AutoRemoveTikTokWatermark == value)
            {
                return;
            }

            _settings.AutoRemoveTikTokWatermark = value;
            OnPropertyChanged();
            SaveSettingsSafely();
        }
    }

    public bool TrimBoundarySilence
    {
        get => _settings.TrimBoundarySilence;
        set
        {
            if (_settings.TrimBoundarySilence == value)
            {
                return;
            }

            _settings.TrimBoundarySilence = value;
            OnPropertyChanged();
            SaveSettingsSafely();
        }
    }

    public bool AlwaysOpenMaximized
    {
        get => _settings.AlwaysOpenMaximized;
        set
        {
            if (_settings.AlwaysOpenMaximized == value)
            {
                return;
            }

            _settings.AlwaysOpenMaximized = value;
            OnPropertyChanged();
            SaveSettingsSafely();
        }
    }

    // MEDIADOCK_QUEUE_BATCH_SETTINGS_R1637
    public IReadOnlyList<string> QueueBatchFormatChoices => QueueDownloadPreferencesService.BatchFormatChoices;
    public IReadOnlyList<int> QueueConcurrencyChoices => QueueDownloadPreferencesService.ConcurrentDownloadChoices;

    public string SelectedQueueBatchFormat
    {
        get => QueueDownloadPreferencesService.NormalizeBatchFormat(_queueDownloadPreferences.BatchFormat);
        set
        {
            var normalized = QueueDownloadPreferencesService.NormalizeBatchFormat(value);
            if (string.Equals(_queueDownloadPreferences.BatchFormat, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _queueDownloadPreferences.BatchFormat = normalized;
            OnPropertyChanged();
            SaveQueueDownloadPreferencesSafelyR1637();
            ApplyQueueBatchFormatPreferenceR1637(DownloadQueue.Where(item => item.CanStart));
        }
    }

    public int QueueConcurrentDownloads
    {
        get => QueueDownloadPreferencesService.NormalizeConcurrency(_queueDownloadPreferences.MaxConcurrentDownloads);
        set
        {
            var normalized = QueueDownloadPreferencesService.NormalizeConcurrency(value);
            if (_queueDownloadPreferences.MaxConcurrentDownloads == normalized)
            {
                return;
            }

            _queueDownloadPreferences.MaxConcurrentDownloads = normalized;
            OnPropertyChanged();
            SaveQueueDownloadPreferencesSafelyR1637();
        }
    }

    public IReadOnlyList<string> ThemeChoices => ThemeService.ThemeNames;

    public string SelectedThemeName
    {
        get => ThemeService.NormalizeThemeName(_settings.ThemeName);
        set
        {
            var normalized = ThemeService.NormalizeThemeName(value);
            if (string.Equals(_settings.ThemeName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _settings.ThemeName = normalized;
            ThemeService.ApplyTheme(normalized);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeDescription));
            SaveSettingsSafely();
            Status = $"Theme changed to {normalized}";
        }
    }

    public string ThemeDescription => ThemeService.GetDescription(SelectedThemeName);

    public AsyncRelayCommand AnalyzeCommand { get; }
    public AsyncRelayCommand DownloadCommand { get; }
    public AsyncRelayCommand ImportLinksCommand { get; }
    public AsyncRelayCommand PasteStreamUrlCommand { get; }
    public AsyncRelayCommand ResolveStreamCommand { get; }
    public AsyncRelayCommand RefreshHealthCommand { get; }
    public AsyncRelayCommand OpenDownloadsCommand { get; }
    public AsyncRelayCommand BrowseOutputDirectoryCommand { get; }
    public AsyncRelayCommand PasteUrlCommand { get; }
    public AsyncRelayCommand BrowseConversionInputCommand { get; }
    public AsyncRelayCommand ConvertLocalFileCommand { get; }
    public AsyncRelayCommand ClearCompletedCommand { get; }
    public AsyncRelayCommand SelectAllQueueCommand { get; }
    public AsyncRelayCommand ClearQueueSelectionCommand { get; }
    public AsyncRelayCommand DownloadAllQueueCommand { get; }
    public AsyncRelayCommand DownloadSelectedQueueCommand { get; }
    public AsyncRelayCommand RemoveSelectedQueueCommand { get; }
    public AsyncRelayCommand SelectAllPlaylistCommand { get; }
    public AsyncRelayCommand ClearPlaylistSelectionCommand { get; }
    public AsyncRelayCommand PreviousPlaylistPageCommand { get; }
    public AsyncRelayCommand NextPlaylistPageCommand { get; }
    public AsyncRelayCommand PreviousQueuePageCommand { get; }
    public AsyncRelayCommand NextQueuePageCommand { get; }

    public AsyncRelayCommand DownloadSubtitlesCommand { get; }
    public async Task InitializeAsync() => await RefreshHealthAsync();

    public void ReportStartupWarning(string message, Exception exception)
    {
        Status = message;
        Diagnostics = exception.ToString();
    }

    public void SetConversionInputPath(string path)
    {
        if (!PremiumWorkspacesEnabled)
        {
            Status = "Convert is disabled during the MediaDock trial. Activate a license to use local conversion.";
            return;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var extension = Path.GetExtension(path);
        if (!new[] { ".mkv", ".mp4", ".webm", ".mov", ".m4v", ".avi", ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".opus" }
            .Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            Status = "Choose a supported video or audio file.";
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

    private bool CanAnalyze() => !Busy && !IsTrialExhausted;

    private bool CanDownload()
    {
        if (Busy || SelectedDownloadFormat is null || IsTrialExhausted)
        {
            return false;
        }

        if (_playlist is not null)
        {
            return PlaylistSelectedCount > 0 &&
                   CanCreateTrialOutput(SelectedDownloadFormat.Kind);
        }

        if (_activeQueueItem?.CanStart != true ||
            !CanCreateTrialOutput(_activeQueueItem.OutputKind))
        {
            return false;
        }

        return _activeQueueItem.OutputKind == OutputFormatKind.Mp3
            ? _activeQueueItem.Mp3BitrateKbps > 0
            : true;
    }

    private bool CanSelectAllPlaylist() =>
        !Busy && IsPlaylistReady && PlaylistSelectedCount < PlaylistTotalCount;

    private bool CanClearPlaylistSelection() =>
        !Busy && IsPlaylistReady && PlaylistSelectedCount > 0;

    private Task SelectAllPlaylistAsync()
    {
        SetAllPlaylistSelection(true);
        return Task.CompletedTask;
    }

    private Task ClearPlaylistSelectionAsync()
    {
        SetAllPlaylistSelection(false);
        return Task.CompletedTask;
    }

    private Task PreviousPlaylistPageAsync()
    {
        if (CanGoPreviousPlaylistPage)
        {
            _playlistPageIndex--;
            RefreshPlaylistPage();
        }
        return Task.CompletedTask;
    }

    private Task NextPlaylistPageAsync()
    {
        if (CanGoNextPlaylistPage)
        {
            _playlistPageIndex++;
            RefreshPlaylistPage();
        }
        return Task.CompletedTask;
    }

    private void RefreshPlaylistPage()
    {
        var maxPageIndex = Math.Max(0, PlaylistPageCount - 1);
        _playlistPageIndex = Math.Min(Math.Max(0, _playlistPageIndex), maxPageIndex);

        PlaylistPageEntries.Clear();
        foreach (var item in PlaylistEntries.Skip(_playlistPageIndex * PlaylistPageSize).Take(PlaylistPageSize))
        {
            PlaylistPageEntries.Add(item);
        }

        OnPropertyChanged(nameof(PlaylistPageCount));
        OnPropertyChanged(nameof(PlaylistPageNumber));
        OnPropertyChanged(nameof(PlaylistPageSummary));
        OnPropertyChanged(nameof(CanGoPreviousPlaylistPage));
        OnPropertyChanged(nameof(CanGoNextPlaylistPage));
        PreviousPlaylistPageCommand.RaiseCanExecuteChanged();
        NextPlaylistPageCommand.RaiseCanExecuteChanged();
    }

    private void SetAllPlaylistSelection(bool selected)
    {
        _batchUpdatingPlaylistSelection = true;
        try
        {
            foreach (var item in PlaylistEntries)
            {
                item.IsSelected = selected;
            }
        }
        finally
        {
            _batchUpdatingPlaylistSelection = false;
        }

        RefreshPlaylistSelectionState();
    }

    private void LoadPlaylistEntries(PlaylistInfo playlist)
    {
        ClearPlaylistEntries();
        _playlistPageIndex = 0;
        foreach (var entry in playlist.Entries)
        {
            var item = new PlaylistSelectionItem(entry);
            item.PropertyChanged += PlaylistEntry_PropertyChanged;
            PlaylistEntries.Add(item);
        }

        RefreshPlaylistSelectionState();
        RefreshPlaylistPage();
    }

    private void ClearPlaylistEntries()
    {
        foreach (var item in PlaylistEntries)
        {
            item.PropertyChanged -= PlaylistEntry_PropertyChanged;
        }

        PlaylistEntries.Clear();
        _playlistPageIndex = 0;
        RefreshPlaylistSelectionState();
        RefreshPlaylistPage();
    }

    private void PlaylistEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_batchUpdatingPlaylistSelection && string.Equals(e.PropertyName, nameof(PlaylistSelectionItem.IsSelected), StringComparison.Ordinal))
        {
            RefreshPlaylistSelectionState();
        }
    }

    private void RefreshPlaylistSelectionState()
    {
        OnPropertyChanged(nameof(IsPlaylistReady));
        OnPropertyChanged(nameof(PlaylistSelectedCount));
        OnPropertyChanged(nameof(PlaylistTotalCount));
        OnPropertyChanged(nameof(PlaylistSelectionSummary));
        OnPropertyChanged(nameof(DownloadButtonText));
        DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
        SelectAllPlaylistCommand.RaiseCanExecuteChanged();
        ClearPlaylistSelectionCommand.RaiseCanExecuteChanged();
        PreviousPlaylistPageCommand.RaiseCanExecuteChanged();
        NextPlaylistPageCommand.RaiseCanExecuteChanged();
    }

    private bool CanConvertLocalFile() =>
        !Busy &&
        PremiumWorkspacesEnabled &&
        !string.IsNullOrWhiteSpace(ConversionInputPath) &&
        File.Exists(ConversionInputPath) &&
        SelectedConversionBitrate is not null;

    private bool CanCreateTrialOutput(OutputFormatKind outputKind) =>
        !IsTrialMode || _trialStateService.CanCreate(outputKind, _trialState);
    private bool EnsureTrialAvailable(OutputFormatKind outputKind)
    {
        if (!IsTrialMode)
        {
            return true;
        }

        if (CanCreateTrialOutput(outputKind))
        {
            return true;
        }

        Status = outputKind == OutputFormatKind.Mp4
            ? "The 5-video trial allowance has been used. A MediaDock license is required for more video outputs."
            : "The 5-MP3 trial allowance has been used. A MediaDock license is required for more MP3 outputs.";

        NotifyTrialStateChanged();
        return false;
    }
    private void RecordTrialCompletion(OutputFormatKind outputKind)
    {
        if (!IsTrialMode)
        {
            return;
        }

        lock (_trialAccountingGateR1630)
        {
            _trialState = _trialStateService.RecordCompletion(outputKind);
        }

        NotifyTrialStateChanged();

        if (IsTrialExhausted)
        {
            Status = "Trial complete. A MediaDock license is required to continue.";
        }
    }

    private void NotifyTrialStateChanged()
    {
        OnPropertyChanged(nameof(TrialVideoUsed));
        OnPropertyChanged(nameof(TrialMp3Used));
        OnPropertyChanged(nameof(TrialVideoRemaining));
        OnPropertyChanged(nameof(TrialMp3Remaining));
        OnPropertyChanged(nameof(IsVideoTrialExhausted));
        OnPropertyChanged(nameof(IsMp3TrialExhausted));
        OnPropertyChanged(nameof(IsTrialExhausted));
        OnPropertyChanged(nameof(TrialTamperDetected));
        OnPropertyChanged(nameof(TrialSummary));
        OnPropertyChanged(nameof(TrialLockTitle));
        OnPropertyChanged(nameof(TrialLockMessage));
        OnPropertyChanged(nameof(DownloadButtonText));

        AnalyzeCommand.RaiseCanExecuteChanged();
        DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
        PasteUrlCommand.RaiseCanExecuteChanged();
        ImportLinksCommand.RaiseCanExecuteChanged();
        PasteStreamUrlCommand.RaiseCanExecuteChanged();
        ResolveStreamCommand.RaiseCanExecuteChanged();
        BrowseConversionInputCommand.RaiseCanExecuteChanged();
        ConvertLocalFileCommand.RaiseCanExecuteChanged();
    }

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

    private bool CanResolveStream()
    {
        if (Busy || !PremiumWorkspacesEnabled)
        {
            return false;
        }

        return Uri.TryCreate(StreamUrl.Trim(), UriKind.Absolute, out var uri) &&
               uri.Scheme is "http" or "https";
    }

    private async Task PasteStreamUrlAsync()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var pasted = Clipboard.GetText().Trim();
                if (string.Equals(StreamUrl.Trim(), pasted, StringComparison.Ordinal))
                {
                    ScheduleAutoStreamResolve(pasted, immediate: true);
                }
                else
                {
                    StreamUrl = pasted;
                }
            }
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            StreamStatus = "Clipboard access failed.";
        }

        await Task.CompletedTask;
    }

    private void ScheduleAutoStreamResolve(string value, bool immediate = false)
    {
        _streamAutoResolveCts?.Cancel();
        _streamAutoResolveCts?.Dispose();
        _streamAutoResolveCts = null;
        _streamResolveCts?.Cancel();

        var candidate = value.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            ResetStreamSurface();
            return;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            uri.Scheme is not "http" and not "https" ||
            IsTrialExhausted)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _streamAutoResolveCts = cts;
        _ = AutoResolveStreamAfterDelayAsync(
            candidate,
            immediate ? 75 : 550,
            cts.Token);
    }

    private async Task AutoResolveStreamAfterDelayAsync(
        string candidate,
        int delayMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delayMilliseconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(StreamUrl.Trim(), candidate, StringComparison.Ordinal))
            {
                return;
            }

            await AnalyzeStreamSourceAsync(candidate, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            StreamStatus = "Automatic Stream analysis failed. Press Detect & Play to use the internal browser detector.";
        }
    }

    private async Task ResolveStreamAsync()
    {
        await AnalyzeStreamSourceAsync(StreamUrl.Trim(), CancellationToken.None);
    }

    private async Task AnalyzeStreamSourceAsync(
        string rawUrl,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not "http" and not "https")
        {
            return;
        }

        var version = Interlocked.Increment(ref _streamResolveVersion);
        _streamResolveCts?.Cancel();
        _streamResolveCts?.Dispose();
        _streamResolveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _streamResolveCts.Token;

        Busy = true;
        _streamMedia = null;
        StreamResolvedUrl = string.Empty;
        IsStreamReady = false;
        StreamQualityChoices.Clear();
        StreamQualityChoices.Add(new QualityChoice(
            QualityChoiceKind.Best,
            null,
            "Auto - Internal media detector"));
        SelectedStreamQuality = StreamQualityChoices[0];

        StreamTitle = "Inspecting media source...";
        StreamPlatform = "MediaDock detector";
        StreamUploader = string.Empty;
        StreamThumbnailUrl = string.Empty;
        StreamDurationText = string.Empty;
        StreamKindText = "Analyzing";
        StreamStatus = "Inspecting the page and available stream qualities...";
        StreamTechnicalText = "Auto mode can use the embedded browser/network detector even when yt-dlp does not recognize the website.";

        try
        {
            var normalized = YtDlpService.NormalizeUserUrl(rawUrl);
            var result = await _mediaEngine.AnalyzeAsync(normalized, token);
            token.ThrowIfCancellationRequested();

            if (version != _streamResolveVersion ||
                !string.Equals(StreamUrl.Trim(), rawUrl, StringComparison.Ordinal))
            {
                return;
            }

            Diagnostics = result.Diagnostics;

            if (result.Media is null)
            {
                StreamTitle = result.Playlist?.Title ?? "Media page";
                StreamPlatform = result.Playlist is null
                    ? "Browser detector"
                    : FriendlyExtractorName(result.Playlist.Extractor);
                StreamUploader = result.Playlist?.Uploader ?? string.Empty;
                StreamThumbnailUrl = result.Playlist?.ThumbnailUrl ?? string.Empty;
                StreamKindText = "Page/collection detected";
                StreamStatus = "Press Detect & Play. MediaDock will inspect the webpage's actual media requests.";
                return;
            }

            _streamMedia = result.Media;
            StreamTitle = result.Media.Title;
            StreamPlatform = FriendlyExtractorName(result.Media.Extractor);
            StreamUploader = string.IsNullOrWhiteSpace(result.Media.Uploader)
                ? "Unknown creator"
                : result.Media.Uploader;
            StreamThumbnailUrl = result.Media.ThumbnailUrl;
            StreamDurationText = FormatDuration(result.Media.DurationSeconds);
            StreamKindText = result.Media.Formats.Any(format => format.HasVideo && !format.IsDrm)
                ? "Video source detected"
                : "Audio source detected";

            StreamQualityChoices.Clear();
            foreach (var choice in YtDlpService.BuildStreamQualityChoices(result.Media))
            {
                StreamQualityChoices.Add(choice);
            }

            SelectedStreamQuality =
                StreamQualityChoices.FirstOrDefault(choice =>
                    choice.Kind == QualityChoiceKind.ExactHeight &&
                    choice.Height == 720)
                ?? StreamQualityChoices.FirstOrDefault();

            var directCount = StreamQualityChoices.Count(choice =>
                choice.Kind == QualityChoiceKind.ExactHeight);

            StreamStatus = directCount > 0
                ? $"Ready - {directCount} direct stream resolution(s) + Auto browser detection"
                : "Ready - Auto will use the internal browser/network detector";

            StreamTechnicalText = directCount > 0
                ? "Choose a direct resolution, or Auto to let MediaDock detect and isolate the webpage video."
                : "No combined direct resolution was exposed. Auto uses the webpage's own player while MediaDock hides surrounding page UI.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (MediaEngineException ex)
        {
            if (version != _streamResolveVersion)
            {
                return;
            }

            Diagnostics = ex.Diagnostics;
            _streamMedia = null;
            StreamTitle = "Webpage ready for internal detection";
            StreamPlatform = "Browser/network detector";
            StreamUploader = string.Empty;
            StreamKindText = "yt-dlp unsupported - browser fallback available";
            StreamStatus = "Press Detect & Play. MediaDock will load the page internally and watch its media traffic.";
            StreamTechnicalText = "This fallback is designed for sites where media is created dynamically by JavaScript.";
        }
        catch (Exception ex)
        {
            if (version != _streamResolveVersion)
            {
                return;
            }

            Diagnostics = ex.ToString();
            _streamMedia = null;
            StreamTitle = "Webpage ready for internal detection";
            StreamPlatform = "Browser/network detector";
            StreamKindText = "Browser fallback available";
            StreamStatus = "Press Detect & Play to inspect this page inside MediaDock.";
        }
        finally
        {
            if (version == _streamResolveVersion)
            {
                Busy = false;
                _streamResolveCts?.Dispose();
                _streamResolveCts = null;
            }
        }
    }

    public async Task<StreamPlaybackRequest?> PrepareStreamPlaybackAsync()
    {
        var rawUrl = StreamUrl.Trim();
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not "http" and not "https")
        {
            StreamStatus = "Paste a valid http or https media/page link.";
            return null;
        }

        if (IsTrialExhausted)
        {
            StreamStatus = "Trial complete. A MediaDock license is required to stream.";
            return null;
        }

        var selected = SelectedStreamQuality;

        if (_streamMedia is not null &&
            selected?.Kind == QualityChoiceKind.ExactHeight)
        {
            try
            {
                Busy = true;
                StreamStatus = $"Resolving {selected.Label}...";
                var resolved = await _mediaEngine.ResolveStreamAsync(
                    rawUrl,
                    selected,
                    CancellationToken.None);

                StreamResolvedUrl = resolved.DirectUrl;
                IsStreamReady = true;
                StreamKindText = resolved.HasVideo
                    ? $"Video - {selected.Label}"
                    : "Audio stream";
                StreamStatus = $"Direct stream ready - {selected.Label}";

                return new StreamPlaybackRequest(
                    rawUrl,
                    resolved.DirectUrl,
                    UseInternalPageCapture: false,
                    $"Direct {selected.Label}");
            }
            catch (MediaEngineException ex)
            {
                Diagnostics = ex.Diagnostics;
                StreamStatus = "Direct resolution could not be opened. Falling back to internal webpage media detection.";
            }
            catch (Exception ex)
            {
                Diagnostics = ex.ToString();
                StreamStatus = "Direct resolution failed. Falling back to internal webpage media detection.";
            }
            finally
            {
                Busy = false;
            }
        }

        StreamResolvedUrl = string.Empty;
        IsStreamReady = false;
        StreamStatus = "Loading webpage internally and watching for media...";
        StreamKindText = "Internal media capture";

        return new StreamPlaybackRequest(
            rawUrl,
            string.Empty,
            UseInternalPageCapture: true,
            "Internal webpage media capture");
    }

    public async Task DownloadStreamAsync(OutputFormatKind outputKind)
    {
        if (!PremiumWorkspacesEnabled)
        {
            StreamStatus = "Stream is disabled during the MediaDock trial. Activate a license to use Stream.";
            return;
        }

        if (Busy)
        {
            return;
        }

        if (!EnsureTrialAvailable(outputKind))
        {
            return;
        }

        var rawUrl = StreamUrl.Trim();
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not "http" and not "https")
        {
            StreamStatus = "Paste a valid media URL before downloading.";
            return;
        }

        MediaInfo? media = _streamMedia;
        if (media is null)
        {
            try
            {
                Busy = true;
                StreamStatus = "Resolving media for download...";
                var analysis = await _mediaEngine.AnalyzeAsync(rawUrl);
                Diagnostics = analysis.Diagnostics;
                media = analysis.Media;
            }
            catch (MediaEngineException ex)
            {
                Diagnostics = ex.Diagnostics;
                StreamStatus = "This detected webpage could not be handed to the download engine.";
                return;
            }
            finally
            {
                Busy = false;
            }
        }

        if (media is null)
        {
            StreamStatus = "No individual downloadable media item was resolved.";
            return;
        }

        var quality = outputKind == OutputFormatKind.Mp3
            ? null
            : SelectedStreamQuality?.Kind == QualityChoiceKind.ExactHeight
                ? SelectedStreamQuality
                : YtDlpService.SelectPreferredDefaultQuality(
                    YtDlpService.BuildQualityChoices(media));

        var audio = YtDlpService.SelectPreferredDefaultAudio(
            YtDlpService.BuildAudioChoices(media));

        var item = new DownloadQueueItem(
            media.Title,
            FriendlyExtractorName(media.Extractor),
            media.WebpageUrl,
            outputKind == OutputFormatKind.Mp3
                ? "Best audio"
                : quality?.Label ?? "Auto - Best available",
            outputKind == OutputFormatKind.Mp3 ? "MP3" : "MP4",
            outputKind,
            media.ThumbnailUrl,
            SelectedMp3Bitrate?.KilobitsPerSecond ?? 320)
        {
            MediaSnapshot = media,
            QualityChoice = quality,
            AudioChoice = audio,
            DurationSeconds = media.DurationSeconds,
            Status = "Ready",
            ProgressText = "Started from Stream"
        };

        DownloadQueue.Insert(0, item);
        _queuePageIndex = 0;
        RefreshQueuePage();
        await DownloadQueueItemAsync(item);
    }

    private void ResetStreamSurface()
    {
        Interlocked.Increment(ref _streamResolveVersion);
        _streamAutoResolveCts?.Cancel();
        _streamAutoResolveCts?.Dispose();
        _streamAutoResolveCts = null;
        _streamResolveCts?.Cancel();
        _streamResolveCts?.Dispose();
        _streamResolveCts = null;

        _streamMedia = null;
        StreamResolvedUrl = string.Empty;
        IsStreamReady = false;
        StreamQualityChoices.Clear();
        StreamQualityChoices.Add(new QualityChoice(
            QualityChoiceKind.Best,
            null,
            "Auto - Internal media detector"));
        SelectedStreamQuality = StreamQualityChoices[0];

        StreamTitle = "Paste a media link to detect video";
        StreamPlatform = "Ready";
        StreamUploader = "Paste a public or accessible webpage/media link.";
        StreamThumbnailUrl = string.Empty;
        StreamDurationText = string.Empty;
        StreamKindText = "Waiting for a link";
        StreamStatus = "Paste a link, choose Auto or a direct resolution, then Detect & Play.";
        StreamTechnicalText = "Auto uses MediaDock's internal browser/network detector.";
    }

    public void ReportStreamPlaybackOpened(string detail = "Playing")
    {
        IsStreamReady = true;
        StreamStatus = detail;
    }

    public void ReportStreamPlaybackEnded()
    {
        StreamStatus = "Playback ended";
    }

    public void ReportStreamPlaybackMessage(string message)
    {
        StreamStatus = message;
    }

    public void ReportCapturedMedia(WebMediaCandidate candidate, int candidateCount)
    {
        StreamKindText = candidate.KindLabel;
        StreamTechnicalText =
            $"{candidate.KindLabel} detected from page traffic - {candidate.ContentType}";
        StreamStatus = $"Media detected - {candidateCount} candidate(s) observed";
    }

    public void ReportStreamPlaybackFailure(string message, Exception? exception = null)
    {
        IsStreamReady = false;
        StreamStatus = message;
        if (exception is not null)
        {
            Diagnostics = exception.ToString();
        }
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
        _activeQueueItem = null;
        OnPropertyChanged(nameof(DownloadButtonText));
        ClearPlaylistEntries();
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
                AudioSummary = "Best available audio per video";
                MediaCapabilityText = $"Playlist - {result.Playlist.Entries.Count}";
                IsMediaReady = true;

                AutoQueuePlaylist(result.Playlist, Platform);
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
                : $"Available: {string.Join(" - ", qualityLabels)}";

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
            QueueAnalyzedSingleMedia(result.Media, normalizedUrl);
            Status = "Added to queue - ready to download";
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
            AnalysisErrorText = "MediaDock encountered unexpected metadata. View technical details in Diagnostics.";
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
        ClearPlaylistEntries();
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

    private bool CanDownloadSubtitlesR1628() =>
        !Busy &&
        _media is not null &&
        _playlist is null;

    private async Task DownloadSubtitlesR1628Async()
    {
        if (_media is null ||
            _playlist is not null)
        {
            Status =
                "Analyze one public video first, then choose Download Subtitles.";

            return;
        }

        Busy =
            true;

        Diagnostics =
            string.Empty;

        try
        {
            Status =
                "Downloading subtitles/captions...";

            var files =
                await _mediaEngine.DownloadSubtitlesAsync(
                    _media,
                    OutputDirectory);

            var completed =
                files.ToArray();

            LastDownloadedFile =
                completed.FirstOrDefault()
                ?? string.Empty;

            Status =
                completed.Length == 1
                    ? "Downloaded 1 subtitle file"
                    : "Downloaded " +
                      completed.Length +
                      " subtitle files";
        }
        catch (MediaEngineException ex)
        {
            Diagnostics =
                ex.Diagnostics;

            Status =
                MediaDownloader.Core.Services.UiTextSanitizer.NormalizeLabel(
                    ex.Message);
        }
        catch (Exception ex)
        {
            Diagnostics =
                ex.ToString();

            Status =
                MediaDownloader.Core.Services.UiTextSanitizer.NormalizeLabel(
                    ex.Message);
        }
        finally
        {
            Busy =
                false;
        }
    }
    private async Task DownloadAsync()
    {
        if (_activeQueueItem is not null)
        {
            await DownloadQueueItemAsync(_activeQueueItem);
        }
    }
    public Task DownloadQueueItemAsync(DownloadQueueItem item) =>
        DownloadQueueItemCoreR1630Async(item, manageGlobalBusyR1630: true);

    private Task DownloadQueueItemConcurrentR1630Async(DownloadQueueItem item) =>
        DownloadQueueItemCoreR1630Async(item, manageGlobalBusyR1630: false);


    private async Task DownloadQueueItemCoreR1630Async(DownloadQueueItem item, bool manageGlobalBusyR1630)
    {
        if ((manageGlobalBusyR1630 && Busy) || item.Completed || string.IsNullOrWhiteSpace(item.SourceUrl))
        {
            return;
        }

        if (!EnsureTrialAvailable(item.OutputKind))
        {
            return;
        }

        try
        {
            if (manageGlobalBusyR1630) Busy = true;
            Interlocked.Exchange(ref _lastDownloadProgressUiTick, 0);
            LastDownloadedFile = string.Empty;
            Diagnostics = string.Empty;

            item.Status = "Analyzing source";
            item.ProgressText = "Preparing download";
            item.ProgressPercent = 0;
            item.SpeedText = string.Empty;

            var media = item.MediaSnapshot;
            var hadMediaSnapshotBeforeDownload = media is not null;
            if (media is null)
            {
                var analysis = await _mediaEngine.AnalyzeAsync(item.SourceUrl);
                Diagnostics = analysis.Diagnostics;
                media = analysis.Media;
                if (media is null)
                {
                    throw new MediaEngineException(
                        "The queued source could not be resolved as an individual media item.",
                        analysis.Diagnostics);
                }
                item.MediaSnapshot = media;
            }

            item.DurationSeconds = media.DurationSeconds;
            if (!item.TryResolveClipRangeR1637(
                    out var clipStartSecondsR1637,
                    out var clipEndSecondsR1637,
                    out var clipErrorR1637))
            {
                item.Status = "Ready";
                item.ProgressText = clipErrorR1637;
                Status = clipErrorR1637;
                return;
            }

            if (!string.IsNullOrWhiteSpace(media.ThumbnailUrl))
            {
                item.ThumbnailUrl = media.ThumbnailUrl;
            }

            if (!string.IsNullOrWhiteSpace(media.Title) &&
                item.Title.StartsWith("Queued media  - ", StringComparison.OrdinalIgnoreCase))
            {
                item.Title = media.Title;
            }

            var qualityChoices = YtDlpService.BuildQualityChoices(media);
            var audioChoices = YtDlpService.BuildAudioChoices(media);
            var quality = ResolveQueuedQuality(item, qualityChoices);
            var audio = ResolveQueuedAudio(item, audioChoices);

            item.QualityChoice = quality;
            item.AudioChoice = audio;
            item.Quality = item.OutputKind == OutputFormatKind.Mp3
                ? "Best audio"
                : quality?.Label ?? "Best available";
            item.Format = item.OutputKind == OutputFormatKind.Mp3 ? "MP3" : "MP4";
            item.Status = "Starting";

            WriteDownloadAttemptLog(item);

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

            var path = await _mediaEngine.DownloadAsync(
                media,
                item.OutputKind == OutputFormatKind.Mp3 ? null : quality,
                audio,
                item.OutputKind,
                item.Mp3BitrateKbps,
                OutputDirectory,
                AudioFadeInOut3Seconds,
                TrimBoundarySilence,
                AutoRemoveTikTokWatermark,
                hadMediaSnapshotBeforeDownload ? item.Title : null,
                onProgress: line =>
                {
                    AddProgressDiagnostic(line);
                    if (!manageGlobalBusyR1630 || ShouldPublishDownloadProgress(line))
                    {
                        PostToUi(() => ApplyQueueProgress(item, line));
                    }
                },
                clipStartSeconds: clipStartSecondsR1637,
                clipEndSeconds: clipEndSecondsR1637);

            Diagnostics = string.Join(Environment.NewLine, progressLines);
            LastDownloadedFile = path;
            item.ProgressPercent = 100;
            item.Status = "Completed";
            item.Completed = true;
            item.OutputPath = path;
            item.SpeedText = string.Empty;
            item.ProgressText = !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? FormatBytes(FileInfoLength(path))
                : "Completed";

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                if (!hadMediaSnapshotBeforeDownload)
                {
                    var resolvedTitle = Path.GetFileNameWithoutExtension(path);
                    if (!string.IsNullOrWhiteSpace(resolvedTitle))
                    {
                        item.Title = resolvedTitle;
                    }
                }

                RecordTrialCompletion(item.OutputKind);
            }

            Status = string.IsNullOrWhiteSpace(path)
                ? "Download completed"
                : $"Completed: {Path.GetFileName(path)}";
        }
        catch (MediaEngineException ex)
        {
            Diagnostics = ex.Diagnostics;
            item.Status = "Failed";
            item.ProgressText = "Open Diagnostics for details";
            Status = "Download failed. Open Diagnostics for details.";
            App.WriteCrashLog("DownloadEngineFailure", ex);
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            item.Status = "Failed";
            item.ProgressText = "Open Diagnostics for details";
            Status = "Download failed. Open Diagnostics for details.";
            App.WriteCrashLog("DownloadQueueItemFailure", ex);
        }
        finally
        {
            if (manageGlobalBusyR1630) Busy = false;
            ClearCompletedCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(DownloadButtonText));
            DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
        }
    }

    public Task OpenQueueFileAsync(DownloadQueueItem item)
    {
        RefreshCounterpartAvailability();

        if (item is null || !item.CanOpenFile || !File.Exists(item.OutputPath))
        {
            if (item is not null)
            {
                item.OutputFileAvailable = false;
            }

            Status = "The downloaded file is no longer available on disk.";
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.OutputPath,
                UseShellExecute = true
            });
            Status = $"Opened: {Path.GetFileName(item.OutputPath)}";
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "Could not open the downloaded file.";
        }

        return Task.CompletedTask;
    }

    public Task DeleteQueueItemAsync(DownloadQueueItem item)
    {
        if (item is null)
        {
            return Task.CompletedTask;
        }

        if (DownloadQueue.Remove(item))
        {
            if (ReferenceEquals(_activeQueueItem, item))
            {
                _activeQueueItem = null;
            }

            _queuePageIndex = Math.Min(_queuePageIndex, Math.Max(0, QueuePageCount - 1));
            RefreshQueuePage();
            OnPropertyChanged(nameof(DownloadButtonText));
            DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
            ClearCompletedCommand.RaiseCanExecuteChanged();
            Status = "Queue item deleted";
        }

        return Task.CompletedTask;
    }

    public Task CopyQueueSourceUrlAsync(DownloadQueueItem item)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(item.SourceUrl))
            {
                Clipboard.SetText(item.SourceUrl);
                Status = "Source URL copied";
            }
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "Could not copy the source URL";
        }

        return Task.CompletedTask;
    }

    public async Task ConvertQueueVideoToMp3Async(DownloadQueueItem item)
    {
        // MEDIADOCK_TRIAL_QUEUE_MP3_R1620_BEGIN
        if (!LicenseEntitlementState.IsLicensed &&
            !TrialStateAdapterR1620.CanCreateMp3Output(this))
        {
            Status = "MP3 trial limit reached (5/5). Buy MediaDock: https://payhip.com/b/oxlUS";
            return;
        }
        // MEDIADOCK_TRIAL_QUEUE_MP3_R1620_END
        RefreshCounterpartAvailability();

        if (!EnsureTrialAvailable(OutputFormatKind.Mp3))
        {
            return;
        }

        if (Busy || !item.CanConvertToMp3)
        {
            if (item.HasDownloadedMp3Counterpart)
            {
                Status = "MP3 is already downloaded for this source.";
            }
            return;
        }
if (!File.Exists(item.OutputPath))
        {
            Status = "The downloaded video file is no longer available on disk.";
            return;
        }

        try
        {
            Busy = true;
            Diagnostics = string.Empty;
            Status = $"Converting {item.Title} to MP3...";
            var outputDirectory = Path.GetDirectoryName(item.OutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = OutputDirectory;
            }

            var log = new List<string>();
            var output = await _conversionService.ConvertToMp3Async(
                item.OutputPath,
                outputDirectory,
                320,
                AudioFadeInOut3Seconds,
                TrimBoundarySilence,
                item.ThumbnailUrl,
                onOutput: line => log.Add(line),
                onError: line => log.Add(line));

            Diagnostics = string.Join(Environment.NewLine, log);
            LastConvertedFile = output;

            // MEDIADOCK_TRIAL_QUEUE_MP3_R1620_BEGIN
            if (!LicenseEntitlementState.IsLicensed)
            {
                try
                {
                    var accounting =
                        TrialStateAdapterR1620.RecordSuccessfulMp3Output(this);

                    if (!accounting.Allowed)
                    {
                        try
                        {
                            if (File.Exists(output))
                            {
                                File.Delete(output);
                            }
                        }
                        catch
                        {
                        }

                        Status = "MP3 trial limit reached (5/5). Buy MediaDock: https://payhip.com/b/oxlUS";
                        return;
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (File.Exists(output))
                        {
                            File.Delete(output);
                        }
                    }
                    catch
                    {
                    }

                    Diagnostics = ex.ToString();
                    Status = "MP3 trial accounting failed safely. No trial MP3 output was kept.";
                    return;
                }
            }
            // MEDIADOCK_TRIAL_QUEUE_MP3_R1620_END
var converted = new DownloadQueueItem(
                item.Title,
                item.Source,
                item.SourceUrl,
                "Best audio - 320 kbps",
                "MP3",
                OutputFormatKind.Mp3,
                item.ThumbnailUrl,
                320)
            {
                MediaSnapshot = item.MediaSnapshot,
                AudioChoice = item.AudioChoice,
                Completed = true,
                ProgressPercent = 100,
                Status = "Completed",
                ProgressText = File.Exists(output) ? FormatBytes(FileInfoLength(output)) : "Converted",
                OutputPath = output
            };

            DownloadQueue.Insert(0, converted);
            _queuePageIndex = 0;
            RefreshQueuePage();

            if (File.Exists(output))
            {
                RecordTrialCompletion(OutputFormatKind.Mp3);
            }

            Status = $"Converted to MP3: {Path.GetFileName(output)}";
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "MP3 conversion failed. Open Diagnostics for details.";
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task RedownloadQueueMp3AsMp4Async(DownloadQueueItem item)
    {
        RefreshCounterpartAvailability();

        if (!EnsureTrialAvailable(OutputFormatKind.Mp4))
        {
            return;
        }

        if (Busy || !item.CanRedownloadAsMp4)
        {
            if (item.HasDownloadedMp4Counterpart)
            {
                Status = "MP4 is already downloaded for this source.";
            }
            return;
        }

        var job = new DownloadQueueItem(
            item.Title,
            item.Source,
            item.SourceUrl,
            "Auto 1080p -> 720p -> 480p",
            "MP4",
            OutputFormatKind.Mp4,
            item.ThumbnailUrl)
        {
            MediaSnapshot = item.MediaSnapshot,
            Status = "Ready",
            ProgressText = "MP4 re-download"
        };

        DownloadQueue.Insert(0, job);
        _queuePageIndex = 0;
        RefreshQueuePage();
        await DownloadQueueItemAsync(job);
    }

    private void AutoQueuePlaylist(PlaylistInfo playlist, string platform)
    {
        _playlist = null;
        ClearPlaylistEntries();

        var outputKind = SelectedDownloadFormat?.Kind ?? OutputFormatKind.Mp4;
        var licensedR1630 = LicenseEntitlementState.IsLicensed;
        var trialRemaining = outputKind == OutputFormatKind.Mp3
            ? TrialMp3Remaining
            : TrialVideoRemaining;
        var alreadyQueued = DownloadQueue.Count(item =>
            item.OutputKind == outputKind &&
            !item.Completed &&
            !string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase));
        var capacity = CalculateTrialQueueCapacityR1630(licensedR1630, trialRemaining, alreadyQueued);

        var quality = outputKind == OutputFormatKind.Mp3
            ? "Best audio"
            : "Auto 1080p -> 720p -> 480p";
        var format = outputKind == OutputFormatKind.Mp3 ? "MP3" : "MP4";
        var bitrate = SelectedMp3Bitrate?.KilobitsPerSecond ?? 320;
        var existing = new HashSet<string>(
            DownloadQueue
                .Where(item => !string.IsNullOrWhiteSpace(item.SourceUrl) &&
                               !string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.SourceUrl),
            StringComparer.OrdinalIgnoreCase);

        var jobs = new List<DownloadQueueItem>();
        var duplicateCount = 0;
        var invalidCount = 0;

        foreach (var entry in playlist.Entries)
        {
            if (jobs.Count >= capacity)
            {
                break;
            }

            var entryUrl = ResolvePlaylistEntryUrl(entry, platform);
            if (string.IsNullOrWhiteSpace(entryUrl))
            {
                invalidCount++;
                continue;
            }

            var normalized = YtDlpService.NormalizeUserUrl(entryUrl);
            if (!existing.Add(normalized))
            {
                duplicateCount++;
                continue;
            }

            jobs.Add(new DownloadQueueItem(
                entry.Title,
                platform,
                normalized,
                quality,
                format,
                outputKind,
                ResolvePlaylistThumbnail(entry, platform),
                bitrate)
            {
                Status = "Ready",
                ProgressText = "Queued automatically from playlist"
            });
        }

        // Insert in reverse so playlist item 1 remains the first new row.
        foreach (var job in jobs.AsEnumerable().Reverse())
        {
            DownloadQueue.Insert(0, job);
        }

        _activeQueueItem = jobs.FirstOrDefault();
        RefreshQueuePage();
        ClearUrlEntryAfterQueue();
        OnPropertyChanged(nameof(DownloadButtonText));
        DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();

        if (licensedR1630)
        {
            Status = jobs.Count == 0
                ? "Playlist detected, but no new valid items were available to queue."
                : $"Added {jobs.Count} playlist item(s) directly to the queue";
            return;
        }
        var mediaLabel = outputKind == OutputFormatKind.Mp3 ? "MP3" : "video";
        var skippedByTrial = Math.Max(0, playlist.Entries.Count - jobs.Count - duplicateCount - invalidCount);

        var notice =
            $"MediaDock is in trial mode. The trial allows {TrialStateService.MaxVideoOutputs} successful video downloads and " +
            $"{TrialStateService.MaxMp3Outputs} successful MP3 downloads.\n\n" +
            $"This playlist contains {playlist.Entries.Count} item(s).\n" +
            $"{mediaLabel} trial remaining: {trialRemaining}.\n" +
            $"Already queued for this format: {alreadyQueued}.\n" +
            $"Added now: {jobs.Count}.";

        if (skippedByTrial > 0)
        {
            notice += $"\nSkipped because of the trial queue limit: {skippedByTrial}.";
        }
        if (duplicateCount > 0)
        {
            notice += $"\nDuplicates skipped: {duplicateCount}.";
        }
        if (invalidCount > 0)
        {
            notice += $"\nUnavailable/invalid playlist entries skipped: {invalidCount}.";
        }

        MessageBox.Show(
            notice,
            "MediaDock Trial Playlist Limit",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Status = jobs.Count == 0
            ? $"Playlist detected, but no additional {mediaLabel} trial slots are available."
            : $"Added {jobs.Count} playlist item(s) directly to the queue";
    }

    private static string ResolvePlaylistEntryUrl(PlaylistEntryInfo entry, string platform)
    {
        var entryUrl = entry.WebpageUrl?.Trim() ?? string.Empty;
        if (Uri.TryCreate(entryUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            return uri.AbsoluteUri;
        }

        if (string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(entry.Id))
        {
            return $"https://www.youtube.com/watch?v={Uri.EscapeDataString(entry.Id)}";
        }

        return string.Empty;
    }

    private static string ResolvePlaylistThumbnail(PlaylistEntryInfo entry, string platform)
    {
        if (Uri.TryCreate(entry.ThumbnailUrl, UriKind.Absolute, out var thumbnailUri) &&
            thumbnailUri.Scheme is "http" or "https")
        {
            return thumbnailUri.AbsoluteUri;
        }

        if (string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(entry.Id))
        {
            return $"https://i.ytimg.com/vi/{entry.Id}/hqdefault.jpg";
        }

        return string.Empty;
    }
    private int GetTrialQueueCapacity(OutputFormatKind outputKind)
    {
        var remaining = outputKind == OutputFormatKind.Mp3
            ? TrialMp3Remaining
            : TrialVideoRemaining;
        var pending = DownloadQueue.Count(item =>
            item.OutputKind == outputKind &&
            !item.Completed &&
            !string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase));

        return CalculateTrialQueueCapacityR1630(
            LicenseEntitlementState.IsLicensed,
            remaining,
            pending);
    }

    private bool CanSelectAllQueue() =>
        !Busy && DownloadQueue.Count > 0 && DownloadQueue.Any(item => !item.IsSelected);

    private bool CanClearQueueSelection() =>
        !Busy && DownloadQueue.Any(item => item.IsSelected);

    private bool CanDownloadAllQueue() =>
        !Busy && !_bulkQueueDownloadActive && DownloadQueue.Any(item => item.CanStart);

    private bool CanDownloadSelectedQueue() =>
        !Busy && !_bulkQueueDownloadActive && DownloadQueue.Any(item => item.IsSelected && item.CanStart);

    private bool CanRemoveSelectedQueue() =>
        !Busy && !_bulkQueueDownloadActive && DownloadQueue.Any(item => item.IsSelected);

    private Task SelectAllQueueAsync()
    {
        foreach (var item in DownloadQueue)
        {
            item.IsSelected = true;
        }

        NotifyQueueSelectionChanged();
        return Task.CompletedTask;
    }

    private Task ClearQueueSelectionAsync()
    {
        foreach (var item in DownloadQueue)
        {
            item.IsSelected = false;
        }

        NotifyQueueSelectionChanged();
        return Task.CompletedTask;
    }

    private async Task DownloadAllQueueAsync()
    {
        await DownloadQueueBatchAsync(DownloadQueue.Where(item => item.CanStart).ToArray(), "all ready items");
    }

    private async Task DownloadSelectedQueueAsync()
    {
        await DownloadQueueBatchAsync(
            DownloadQueue.Where(item => item.IsSelected && item.CanStart).ToArray(),
            "selected items");
    }
    private async Task DownloadQueueBatchAsync(IReadOnlyList<DownloadQueueItem> items, string label)
    {
        await DownloadQueueBatchR1630Async(items, label);
    }

    public async Task DownloadQueueBatchR1630Async(
        IEnumerable<DownloadQueueItem> source,
        string label)
    {
        if (Busy || _bulkQueueDownloadActive)
        {
            Status = "A MediaDock task is already running.";
            return;
        }

        var requestedR1637 = source
            .Where(item => item is not null)
            .Distinct()
            .ToArray();
        ApplyQueueBatchFormatPreferenceR1637(requestedR1637);

        var pending = requestedR1637
            .Where(item => item.CanStart)
            .ToList();
        if (pending.Count == 0)
        {
            Status = "There are no ready queue items to download.";
            return;
        }

        _bulkQueueDownloadActive = true;
        Busy = true;
        RaiseQueueBulkCanExecuteChanged();
        var attempted = 0;
        var trialSkipped = 0;

        try
        {
            while (pending.Count > 0)
            {
                var licensed = LicenseEntitlementState.IsLicensed;
                var videoSlots = licensed ? int.MaxValue : TrialVideoRemaining;
                var mp3Slots = licensed ? int.MaxValue : TrialMp3Remaining;
                var wave = new List<DownloadQueueItem>(QueueConcurrentDownloads);

                foreach (var item in pending)
                {
                    if (wave.Count >= QueueConcurrentDownloads)
                    {
                        break;
                    }

                    if (!item.CanStart)
                    {
                        continue;
                    }

                    if (!licensed)
                    {
                        if (item.OutputKind == OutputFormatKind.Mp3)
                        {
                            if (mp3Slots <= 0) continue;
                            mp3Slots--;
                        }
                        else
                        {
                            if (videoSlots <= 0) continue;
                            videoSlots--;
                        }
                    }

                    wave.Add(item);
                }

                if (wave.Count == 0)
                {
                    trialSkipped += pending.Count(item => item.CanStart);
                    break;
                }

                foreach (var item in wave)
                {
                    pending.Remove(item);
                }

                attempted += wave.Count;
                Status = wave.Count == 1
                    ? $"Downloading 1 {label}..."
                    : $"Downloading {wave.Count} {label} concurrently...";

                await Task.WhenAll(wave.Select(DownloadQueueItemConcurrentR1630Async));
            }
        }
        finally
        {
            _bulkQueueDownloadActive = false;
            Busy = false;
            RaiseQueueBulkCanExecuteChanged();
        }

        Status = trialSkipped > 0
            ? $"Batch finished for {label}. {trialSkipped} item(s) were not started because the unlicensed trial allowance is exhausted."
            : $"Batch finished for {label}. Attempted {attempted} item(s), with up to {QueueConcurrentDownloads} simultaneous downloads.";
    }

    private void ApplyQueueBatchFormatPreferenceR1637(IEnumerable<DownloadQueueItem> items)
    {
        var preference = QueueDownloadPreferencesService.NormalizeBatchFormat(SelectedQueueBatchFormat);
        if (string.Equals(preference, QueueDownloadPreferencesService.KeepEachItemFormat, StringComparison.Ordinal))
        {
            return;
        }

        var selectedFormat = string.Equals(
            preference,
            QueueDownloadPreferencesService.AllAsMp3,
            StringComparison.Ordinal)
            ? "MP3"
            : "MP4";

        foreach (var item in items.Where(item => item.CanStart))
        {
            item.SelectedFormatR1629 = selectedFormat;
        }

        PersistQueueSafely();
    }

    private Task RemoveSelectedQueueAsync()
    {
        var selected = DownloadQueue.Where(item => item.IsSelected).ToArray();
        foreach (var item in selected)
        {
            DownloadQueue.Remove(item);
            if (ReferenceEquals(_activeQueueItem, item))
            {
                _activeQueueItem = null;
            }
        }

        NotifyQueueSelectionChanged();
        OnPropertyChanged(nameof(DownloadButtonText));
        DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
        Status = selected.Length == 0
            ? "No queue items were selected."
            : $"Removed {selected.Length} selected queue item(s).";
        return Task.CompletedTask;
    }

    private void NotifyQueueSelectionChanged()
    {
        OnPropertyChanged(nameof(QueueSelectedCount));
        OnPropertyChanged(nameof(QueueSelectionSummary));
        RaiseQueueBulkCanExecuteChanged();
    }

    private void RaiseQueueBulkCanExecuteChanged()
    {
        SelectAllQueueCommand.RaiseCanExecuteChanged();
        ClearQueueSelectionCommand.RaiseCanExecuteChanged();
        DownloadAllQueueCommand.RaiseCanExecuteChanged();
        DownloadSelectedQueueCommand.RaiseCanExecuteChanged();
        RemoveSelectedQueueCommand.RaiseCanExecuteChanged();
    }

    private void QueueAnalyzedSingleMedia(MediaInfo media, string normalizedUrl)
    {
        var existing = DownloadQueue.FirstOrDefault(item =>
            string.Equals(item.SourceUrl, normalizedUrl, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            _activeQueueItem = existing;
            ClearUrlEntryAfterQueue();
            OnPropertyChanged(nameof(DownloadButtonText));
            DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
            return;
        }

        var outputKind = SelectedDownloadFormat?.Kind ?? OutputFormatKind.Mp4;
        var job = new DownloadQueueItem(
            media.Title,
            FriendlyExtractorName(media.Extractor),
            normalizedUrl,
            outputKind == OutputFormatKind.Mp3 ? "Best audio" : SelectedQuality?.Label ?? "Best available",
            outputKind == OutputFormatKind.Mp3 ? "MP3" : "MP4",
            outputKind,
            media.ThumbnailUrl,
            SelectedMp3Bitrate?.KilobitsPerSecond ?? 320)
        {
            MediaSnapshot = media,
            QualityChoice = SelectedQuality,
            AudioChoice = SelectedAudioChoice,
            DurationSeconds = media.DurationSeconds,
            Status = "Ready",
            ProgressText = "Ready to download"
        };

        DownloadQueue.Insert(0, job);
        _queuePageIndex = 0;
        RefreshQueuePage();
        _activeQueueItem = job;
        ClearUrlEntryAfterQueue();
        OnPropertyChanged(nameof(DownloadButtonText));
        DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
    }

    private void ClearUrlEntryAfterQueue()
    {
        _autoAnalyzeCts?.Cancel();
        _autoAnalyzeCts?.Dispose();
        _autoAnalyzeCts = null;
        if (!string.IsNullOrEmpty(_url))
        {
            _url = string.Empty;
            OnPropertyChanged(nameof(Url));
            AnalyzeCommand.RaiseCanExecuteChanged();
        }
    }

    private void SyncActiveQueueSelection()
    {
        if (_activeQueueItem is null || _activeQueueItem.Completed || !string.Equals(_activeQueueItem.Status, "Ready", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var outputKind = SelectedDownloadFormat?.Kind ?? OutputFormatKind.Mp4;
        _activeQueueItem.OutputKind = outputKind;
        _activeQueueItem.Format = outputKind == OutputFormatKind.Mp3 ? "MP3" : "MP4";
        _activeQueueItem.QualityChoice = SelectedQuality;
        _activeQueueItem.AudioChoice = SelectedAudioChoice;
        _activeQueueItem.Mp3BitrateKbps = SelectedMp3Bitrate?.KilobitsPerSecond ?? 320;
        _activeQueueItem.Quality = outputKind == OutputFormatKind.Mp3
            ? "Best audio"
            : SelectedQuality?.Label ?? "Best available";
        OnPropertyChanged(nameof(DownloadButtonText));
    }

    private static QualityChoice? ResolveQueuedQuality(
        DownloadQueueItem item,
        IReadOnlyList<QualityChoice> available)
    {
        if (item.OutputKind == OutputFormatKind.Mp3)
        {
            return null;
        }

        // MEDIADOCK_QUEUE_QUALITY_RESOLUTION_R1629
        var requestedHeightR1629 =
            item.SelectedQualityR1629 switch
            {
                "2160p" => 2160,
                "1440p" => 1440,
                "1080p" => 1080,
                "720p" => 720,
                "480p" => 480,
                "360p" => 360,
                _ => 0
            };

        if (requestedHeightR1629 > 0)
        {
            var requested = available
                .Where(choice =>
                    choice.Kind == QualityChoiceKind.ExactHeight &&
                    choice.Height == requestedHeightR1629)
                .OrderByDescending(choice => choice.Fps ?? 0)
                .FirstOrDefault();

            if (requested is not null)
            {
                return requested;
            }
        }
        if (item.QualityChoice is not null)
        {
            if (!string.IsNullOrWhiteSpace(item.QualityChoice.FormatId))
            {
                var exactId = available.FirstOrDefault(choice => string.Equals(
                    choice.FormatId,
                    item.QualityChoice.FormatId,
                    StringComparison.Ordinal));
                if (exactId is not null)
                {
                    return exactId;
                }
            }

            if (item.QualityChoice.Height is > 0)
            {
                var sameHeight = available
                    .Where(choice => choice.Kind == QualityChoiceKind.ExactHeight && choice.Height == item.QualityChoice.Height)
                    .OrderByDescending(choice => choice.Fps ?? 0)
                    .FirstOrDefault();
                if (sameHeight is not null)
                {
                    return sameHeight;
                }
            }
        }

        return YtDlpService.SelectPreferredDefaultQuality(available);
    }

    private static AudioChoice? ResolveQueuedAudio(
        DownloadQueueItem item,
        IReadOnlyList<AudioChoice> available)
    {
        if (!string.IsNullOrWhiteSpace(item.AudioChoice?.FormatId))
        {
            var exact = available.FirstOrDefault(choice => string.Equals(
                choice.FormatId,
                item.AudioChoice.FormatId,
                StringComparison.Ordinal));
            if (exact is not null)
            {
                return exact;
            }
        }

        return YtDlpService.SelectPreferredDefaultAudio(available);
    }

    private void WriteDownloadAttemptLog(DownloadQueueItem item)
    {
        try
        {
            var directory = App.GetCrashLogDirectory();
            var lines = new[]
            {
                "MediaDock last download attempt",
                $"Timestamp: {DateTimeOffset.Now:O}",
                $"ProcessId: {Environment.ProcessId}",
                $"URL: {item.SourceUrl}",
                $"OutputFormat: {item.Format}",
                $"Quality: {item.QualityChoice?.Label ?? item.Quality}",
                $"QualityFormatId: {item.QualityChoice?.FormatId ?? string.Empty}",
                $"Audio: {item.AudioChoice?.Label ?? string.Empty}",
                $"AudioFormatId: {item.AudioChoice?.FormatId ?? string.Empty}",
                $"Mp3Bitrate: {item.Mp3BitrateKbps}",
                "PlaylistSelected: 0/0",
                "PlaylistIndexes: ",
                $"OutputDirectory: {OutputDirectory}"
            };
            File.WriteAllLines(Path.Combine(directory, "Last-Download-Attempt.txt"), lines);
        }
        catch
        {
            // Diagnostics must never interfere with the download path.
        }
    }

    private async Task ImportLinkFilesFromDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import media links into the MediaDock queue",
            Filter = "Link lists (*.txt;*.csv)|*.txt;*.csv|Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            await ImportLinkFilesAsync(dialog.FileNames);
        }
    }

    public async Task ImportLinkFilesAsync(IEnumerable<string> paths)
    {
        if (Busy || IsTrialExhausted)
        {
            return;
        }

        try
        {
            Busy = true;
            Status = "Reading link list...";
            var urls = await BatchLinkImportService.ReadUrlsFromFilesAsync(paths);
            ImportUrlsToQueue(urls, "Imported list");
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "Could not import the TXT/CSV link list.";
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task AcceptDroppedTextAsync(string text)
    {
        if (Busy || IsTrialExhausted)
        {
            return;
        }

        var urls = BatchLinkImportService.ExtractUrlsFromText(text);
        if (urls.Count == 0)
        {
            Status = "No HTTP or HTTPS media link was found in the dropped content.";
            return;
        }

        if (urls.Count == 1)
        {
            var link = urls[0];
            if (string.Equals(Url.Trim(), link, StringComparison.Ordinal))
            {
                ScheduleAutoAnalyze(link, immediate: true);
            }
            else
            {
                Url = link;
            }

            Status = "Media link accepted - analyzing";
            return;
        }

        ImportUrlsToQueue(urls, "Dropped links");
        await Task.CompletedTask;
    }

    private void ImportUrlsToQueue(IEnumerable<string> urls, string sourceLabel)
    {
        var normalized = new List<string>();
        var incomingSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in urls)
        {
            if (!Uri.TryCreate(raw?.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme is not "http" and not "https")
            {
                continue;
            }

            var candidate = YtDlpService.NormalizeUserUrl(uri.AbsoluteUri);
            if (incomingSeen.Add(candidate))
            {
                normalized.Add(candidate);
            }
        }

        if (normalized.Count == 0)
        {
            Status = "No valid media links were found.";
            return;
        }

        var existing = new HashSet<string>(
            DownloadQueue
                .Where(item => !string.IsNullOrWhiteSpace(item.SourceUrl))
                .Select(item => item.SourceUrl),
            StringComparer.OrdinalIgnoreCase);

        var outputKind = SelectedDownloadFormat?.Kind ?? OutputFormatKind.Mp4;
        var quality = outputKind == OutputFormatKind.Mp3
            ? "Best audio"
            : SelectedQuality?.Label ?? "Auto - Best available";
        var format = outputKind == OutputFormatKind.Mp3 ? "MP3" : "MP4";
        var bitrate = SelectedMp3Bitrate?.KilobitsPerSecond ?? 320;

        var added = 0;
        var duplicates = 0;
        var trialCapacity = GetTrialQueueCapacity(outputKind);
        var trialSkipped = 0;

        // Insert in reverse so the first link in the file remains the first
        // visible imported row at the top of the queue.
        foreach (var url in normalized.AsEnumerable().Reverse())
        {
            if (added >= trialCapacity)
            {
                trialSkipped++;
                continue;
            }

            if (!existing.Add(url))
            {
                duplicates++;
                continue;
            }

            var host = Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                ? parsed.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase)
                : "media";

            var item = new DownloadQueueItem(
                $"Queued media - {host}",
                sourceLabel,
                url,
                quality,
                format,
                outputKind,
                string.Empty,
                bitrate)
            {
                Status = "Ready",
                ProgressText = "Analyzes source when download starts"
            };

            DownloadQueue.Insert(0, item);
            added++;
        }

        if (added > 0)
        {
            _queuePageIndex = 0;
            RefreshQueuePage();
            _activeQueueItem = DownloadQueue.FirstOrDefault();
            OnPropertyChanged(nameof(DownloadButtonText));
            DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
            ClearCompletedCommand.RaiseCanExecuteChanged();
        }

        Status = duplicates > 0
            ? $"Imported {added} link(s) to queue - {duplicates} duplicate(s) skipped"
            : $"Imported {added} link(s) to queue";
    }

    private async Task BrowseOutputDirectoryAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose MediaDock save location",
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
            Title = "Select video or audio to convert to MP3",
            Filter = "Media files (*.mkv;*.mp4;*.webm;*.mov;*.m4v;*.avi;*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus)|*.mkv;*.mp4;*.webm;*.mov;*.m4v;*.avi;*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus|All files (*.*)|*.*",
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
        if (!EnsureTrialAvailable(OutputFormatKind.Mp3))
        {
            return;
        }

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
                AudioFadeInOut3Seconds,
                TrimBoundarySilence,
                null,
                onOutput: line => log.Add(line),
                onError: line => log.Add(line));

            Diagnostics = string.Join(Environment.NewLine, log);
            LastConvertedFile = output;

            if (File.Exists(output))
            {
                RecordTrialCompletion(OutputFormatKind.Mp3);
            }

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

    private void RestorePersistedQueue()
    {
        _restoringQueue = true;
        try
        {
            foreach (var item in _queuePersistenceService.Load())
            {
                DownloadQueue.Add(item);
            }
        }
        finally
        {
            _restoringQueue = false;
        }

        RefreshCounterpartAvailability();

        if (DownloadQueue.Count > 0)
        {
            Status = $"Restored {DownloadQueue.Count} queue item(s)";
        }

        ClearCompletedCommand.RaiseCanExecuteChanged();
    }

    private void DownloadQueue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<DownloadQueueItem>())
            {
                oldItem.PropertyChanged -= QueueItem_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<DownloadQueueItem>())
            {
                newItem.PropertyChanged += QueueItem_PropertyChanged;
            }
        }

        RefreshCounterpartAvailability();
        RefreshQueuePage();
        NotifyQueueSelectionChanged();
        ClearCompletedCommand.RaiseCanExecuteChanged();
        PersistQueueSafely();
    }

    private void QueueItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_restoringQueue)
        {
            return;
        }

        if (e.PropertyName is nameof(DownloadQueueItem.IsSelected))
        {
            NotifyQueueSelectionChanged();
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            e.PropertyName is nameof(DownloadQueueItem.Completed) or
                nameof(DownloadQueueItem.OutputPath) or
                nameof(DownloadQueueItem.OutputKind))
        {
            RefreshCounterpartAvailability();
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            e.PropertyName is nameof(DownloadQueueItem.Title) or
                nameof(DownloadQueueItem.Status) or
                nameof(DownloadQueueItem.OutputPath) or
                nameof(DownloadQueueItem.Completed) or
                nameof(DownloadQueueItem.Quality) or
                nameof(DownloadQueueItem.Format) or
                nameof(DownloadQueueItem.OutputKind) or
                nameof(DownloadQueueItem.Mp3BitrateKbps) or
                nameof(DownloadQueueItem.QualityChoice) or
                nameof(DownloadQueueItem.AudioChoice) or
                nameof(DownloadQueueItem.ThumbnailUrl) or
                nameof(DownloadQueueItem.DurationSeconds) or
                nameof(DownloadQueueItem.ClipStartText) or
                nameof(DownloadQueueItem.ClipEndText) or
                nameof(DownloadQueueItem.ProgressText))
        {
            PersistQueueSafely();
        }
    }

    private void RefreshCounterpartAvailability()
    {
        foreach (var item in DownloadQueue)
        {
            item.OutputFileAvailable = IsQueueOutputAvailable(item);
            item.HasDownloadedMp3Counterpart = HasCompletedArtifact(item, OutputFormatKind.Mp3);
            item.HasDownloadedMp4Counterpart = HasCompletedArtifact(item, OutputFormatKind.Mp4);
        }
    }

    private bool HasCompletedArtifact(DownloadQueueItem sourceItem, OutputFormatKind targetKind)
    {
        return DownloadQueue.Any(other =>
            !ReferenceEquals(other, sourceItem) &&
            other.Completed &&
            other.OutputKind == targetKind &&
            string.Equals(other.SourceUrl, sourceItem.SourceUrl, StringComparison.OrdinalIgnoreCase) &&
            IsQueueOutputAvailable(other));
    }

    private static bool IsQueueOutputAvailable(DownloadQueueItem item)
    {
        return item.Completed &&
               !string.IsNullOrWhiteSpace(item.OutputPath) &&
               File.Exists(item.OutputPath);
    }

    private void PersistQueueSafely()
    {
        if (_restoringQueue || !_queuePersistenceEnabled)
        {
            return;
        }

        try
        {
            _queuePersistenceService.Save(DownloadQueue);
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "Queue history could not be saved. Open Diagnostics for details.";
        }
    }

    private void SaveSettingsSafely()
    {
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "Settings could not be saved. Open Diagnostics for details.";
        }
    }

    private void SaveQueueDownloadPreferencesSafelyR1637()
    {
        try
        {
            _queueDownloadPreferencesService.Save(_queueDownloadPreferences);
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "Queue download settings could not be saved. Open Diagnostics for details.";
        }
    }

    private Task PreviousQueuePageAsync()
    {
        if (CanGoPreviousQueuePage)
        {
            _queuePageIndex--;
            RefreshQueuePage();
        }

        return Task.CompletedTask;
    }

    private Task NextQueuePageAsync()
    {
        if (CanGoNextQueuePage)
        {
            _queuePageIndex++;
            RefreshQueuePage();
        }

        return Task.CompletedTask;
    }

    private void RefreshQueuePage()
    {
        _queuePageIndex = 0;
        QueuePageEntries.Clear();
        foreach (var item in DownloadQueue)
        {
            QueuePageEntries.Add(item);
        }

        OnPropertyChanged(nameof(QueuePageCount));
        OnPropertyChanged(nameof(QueuePageNumber));
        OnPropertyChanged(nameof(QueuePageSummary));
        OnPropertyChanged(nameof(CanGoPreviousQueuePage));
        OnPropertyChanged(nameof(CanGoNextQueuePage));

        PreviousQueuePageCommand.RaiseCanExecuteChanged();
        NextQueuePageCommand.RaiseCanExecuteChanged();
    }

    private async Task ClearCompletedAsync()
    {
        var completed = DownloadQueue.Where(item => item.Completed).ToArray();
        foreach (var item in completed)
        {
            DownloadQueue.Remove(item);
            if (ReferenceEquals(_activeQueueItem, item))
            {
                _activeQueueItem = null;
            }
        }
        OnPropertyChanged(nameof(DownloadButtonText));
        DownloadCommand.RaiseCanExecuteChanged();
                DownloadSubtitlesCommand.RaiseCanExecuteChanged();
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
        if (parts.Length < 5)
        {
            item.Status = "Downloading";
            return;
        }

        var state = parts[1];
        var downloadedBytes = ParseDouble(parts[2]);
        var totalBytes = ParseDouble(parts[3]);
        var speed = ParseDouble(parts[4]);
        var itemPercent = totalBytes > 0 && downloadedBytes >= 0
            ? Math.Clamp(downloadedBytes / totalBytes * 100.0, 0, 100)
            : 0;

        var playlistIndex = parts.Length > 5 ? ParseInt(parts[5]) : 0;
        var playlistCount = parts.Length > 6 ? ParseInt(parts[6]) : 0;
        var playlistTitle = parts.Length > 7 ? parts[7].Trim() : string.Empty;

        if (playlistIndex > 0 && playlistCount > 0)
        {
            item.ProgressPercent = Math.Clamp(((playlistIndex - 1) + itemPercent / 100.0) / playlistCount * 100.0, 0, 100);
            item.ProgressText = $"Item {playlistIndex}/{playlistCount}";
            if (!string.IsNullOrWhiteSpace(playlistTitle) && playlistTitle != "NA")
            {
                item.Status = Truncate($"{playlistIndex}/{playlistCount} - {playlistTitle}", 78);
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
                : "Downloading";
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
        // MEDIADOCK_REDDIT_FRIENDLY_NAME_R1628
        if (extractor.Contains("reddit", StringComparison.OrdinalIgnoreCase)) return "Reddit";
        if (extractor.Contains("generic", StringComparison.OrdinalIgnoreCase)) return "Direct / Web";        if (extractor.Contains("vimeo", StringComparison.OrdinalIgnoreCase)) return "Vimeo";
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
        var bitrate = format.AudioBitrate is > 0 ? $" - {Math.Round(format.AudioBitrate.Value):0} kbps" : string.Empty;
        return $"Best source - {codec}{bitrate}";
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

        return value[..Math.Max(1, maxLength - 1)] + "...";
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

    // MEDIADOCK_QUEUE_WORKSPACE_VM_R1629
    public void SetQueueWorkspaceStatusR1629(string text)
    {
        Status = MediaDownloader.Core.Services.UiTextSanitizer.NormalizeLabel(text);
    }

    public async Task ImportUrlsR1629Async(System.Collections.Generic.IEnumerable<string> rawUrls)
    {
        var urls = rawUrls
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (urls.Length == 0)
        {
            Status = "No media URLs were found in the selected import file.";
            return;
        }

        var before = DownloadQueue.Count;
        foreach (var rawUrl in urls)
        {
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme is not "http" and not "https")
            {
                continue;
            }

            Url = rawUrl;
            _autoAnalyzeCts?.Cancel();
            await AnalyzeUrlAsync(rawUrl, CancellationToken.None);

            if (_playlist is not null && PlaylistSelectedCount > 0)
            {
                // Route playlists through the existing primary Download workflow.
                // This deliberately avoids depending on a private playlist helper name,
                // which changed across verified MediaDock lineages.
                await DownloadAsync();
            }
        }

        var added = Math.Max(0, DownloadQueue.Count - before);
        Status = added == 1
            ? "Imported 1 media item into the download queue."
            : $"Imported {added} media items into the download queue.";
    }

    public async Task DownloadQueueSubtitlesR1629Async(DownloadQueueItem item)
    {
        if (Busy || item is null || string.IsNullOrWhiteSpace(item.SourceUrl))
        {
            return;
        }

        Busy = true;
        Diagnostics = string.Empty;
        try
        {
            Status = $"Downloading subtitles for {item.Title}...";
            var media = item.MediaSnapshot;
            if (media is null)
            {
                var analysis = await _mediaEngine.AnalyzeAsync(item.SourceUrl);
                Diagnostics = analysis.Diagnostics;
                media = analysis.Media;
            }

            if (media is null)
            {
                throw new MediaEngineException(
                    "This queued source could not be resolved as one media item for subtitles.",
                    Diagnostics);
            }

            var files = await _mediaEngine.DownloadSubtitlesAsync(media, OutputDirectory);
            var completed = files.ToArray();
            LastDownloadedFile = completed.FirstOrDefault() ?? string.Empty;
            Status = completed.Length == 1
                ? "Downloaded 1 subtitle file"
                : $"Downloaded {completed.Length} subtitle files";
        }
        catch (MediaEngineException ex)
        {
            Diagnostics = ex.Diagnostics;
            Status = MediaDownloader.Core.Services.UiTextSanitizer.NormalizeLabel(ex.Message);
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = MediaDownloader.Core.Services.UiTextSanitizer.NormalizeLabel(ex.Message);
        }
        finally
        {
            Busy = false;
        }
    }

    private static int CalculateTrialQueueCapacityR1630(
        bool licensed,
        int remaining,
        int alreadyReserved)
    {
        return licensed
            ? int.MaxValue
            : Math.Max(0, remaining - Math.Max(0, alreadyReserved));
    }

    public static void RunEntitlementQueueContractSelfTestR1630()
    {
        if (MaxConcurrentQueueDownloadsR1630 != 5)
        {
            throw new InvalidOperationException("R1.6.30 queue contract failed: concurrency limit must be exactly five.");
        }

        if (CalculateTrialQueueCapacityR1630(true, 2, 0) != int.MaxValue ||
            CalculateTrialQueueCapacityR1630(true, 0, 999) != int.MaxValue)
        {
            throw new InvalidOperationException("R1.6.30 entitlement contract failed: licensed queue capacity must not use trial counters.");
        }

        if (CalculateTrialQueueCapacityR1630(false, 5, 3) != 2 ||
            CalculateTrialQueueCapacityR1630(false, 2, 4) != 0)
        {
            throw new InvalidOperationException("R1.6.30 trial contract failed: unlicensed queue capacity is not strict 5+5 remaining capacity.");
        }
    }
}
