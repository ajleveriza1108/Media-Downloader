using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MediaDownloader.Core.Services;

public enum TorrentPriorityR1644
{
    Lowest,
    Low,
    Normal,
    High,
    Highest,
    Immediate
}

public sealed class TorrentPreferencesR199
{
    public bool RememberLoadedTorrents { get; set; } = true;
    public bool ResumeLoadedTorrents { get; set; } = true;
    public bool AutoStartDownloads { get; set; } = true;
    public bool ConfirmRemove { get; set; } = true;
    public bool EnableIncomingConnections { get; set; } = false;
    public int ListeningPort { get; set; } = 0;
    public bool EnablePortMapping { get; set; } = true;
    public bool EnableDht { get; set; } = true;
    public bool EnablePex { get; set; } = true;
    public bool EnableLocalPeerDiscovery { get; set; } = true;
    public bool UsePublicTrackerFallback { get; set; } = true;
    public bool FastPeerDiscovery { get; set; } = true;
    public bool EnableFastResume { get; set; } = true;
    public int DhtPort { get; set; } = 0;
    public int MaximumDownloadRateKbps { get; set; } = 0;
    public int MaximumUploadRateKbps { get; set; } = 0;
    public int MaximumConnections { get; set; } = 320;
    public int MaximumPeersPerTorrent { get; set; } = 160;
    public int UploadSlotsPerTorrent { get; set; } = 8;
    public int MaximumHalfOpenConnections { get; set; } = 32;
    public int MaximumActiveDownloads { get; set; } = 3;
    public int PeerRecoveryAttempts { get; set; } = 30;
    public int PeerRecoveryIntervalSeconds { get; set; } = 4;
    public string DownloadDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "MediaDock",
        "Torrents");

    public TorrentPreferencesR199 Clone() => new()
    {
        RememberLoadedTorrents = RememberLoadedTorrents,
        ResumeLoadedTorrents = ResumeLoadedTorrents,
        AutoStartDownloads = AutoStartDownloads,
        ConfirmRemove = ConfirmRemove,
        EnableIncomingConnections = EnableIncomingConnections,
        ListeningPort = ListeningPort,
        EnablePortMapping = EnablePortMapping,
        EnableDht = EnableDht,
        EnablePex = EnablePex,
        EnableLocalPeerDiscovery = EnableLocalPeerDiscovery,
        UsePublicTrackerFallback = UsePublicTrackerFallback,
        FastPeerDiscovery = FastPeerDiscovery,
        EnableFastResume = EnableFastResume,
        DhtPort = DhtPort,
        MaximumDownloadRateKbps = MaximumDownloadRateKbps,
        MaximumUploadRateKbps = MaximumUploadRateKbps,
        MaximumConnections = MaximumConnections,
        MaximumPeersPerTorrent = MaximumPeersPerTorrent,
        UploadSlotsPerTorrent = UploadSlotsPerTorrent,
        MaximumHalfOpenConnections = MaximumHalfOpenConnections,
        MaximumActiveDownloads = MaximumActiveDownloads,
        PeerRecoveryAttempts = PeerRecoveryAttempts,
        PeerRecoveryIntervalSeconds = PeerRecoveryIntervalSeconds,
        DownloadDirectory = DownloadDirectory
    };
}

public sealed class TorrentPreviewFileR1644 : INotifyPropertyChanged
{
    private bool _selected = true;
    private TorrentPriorityR1644 _priority = TorrentPriorityR1644.Normal;

    public int Index { get; init; }
    public string Path { get; init; } = string.Empty;
    public long Length { get; init; }
    public string SizeText => TorrentClientR1644.FormatSizeR1644(Length);

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public TorrentPriorityR1644 Priority
    {
        get => _priority;
        set
        {
            if (_priority == value)
            {
                return;
            }
            _priority = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Priority)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TorrentPreviewR1644
{
    internal string PreviewId { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string PersistentSource { get; init; } = string.Empty;
    public string Name { get; init; } = "Torrent";
    public long TotalSize { get; init; }
    public string TotalSizeText => TotalSize > 0 ? TorrentClientR1644.FormatSizeR1644(TotalSize) : "Metadata pending";
    public bool IsMagnet { get; init; }
    public IReadOnlyList<TorrentPreviewFileR1644> Files { get; init; } = Array.Empty<TorrentPreviewFileR1644>();
}

public sealed class TorrentStatusSnapshotR1644
{
    public string Id { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string PersistentSource { get; init; } = string.Empty;
    public string SavePath { get; init; } = string.Empty;
    public string Name { get; init; } = "Torrent";
    public string Status { get; init; } = "Stopped";
    public double Progress { get; init; }
    public double VerifiedProgress { get; init; }
    public bool LiveTransferActive { get; init; }
    public long TotalSize { get; init; }
    public long DownloadRate { get; init; }
    public long UploadRate { get; init; }
    public int Peers { get; init; }
    public int Seeds { get; init; }
    public double EtaSeconds { get; init; } = -1;
    public long Downloaded { get; init; }
    public long Uploaded { get; init; }
    public double Ratio { get; init; }
    public string LastError { get; init; } = string.Empty;
    public string DiscoveryStatus { get; init; } = string.Empty;
    public string LastTrackerStatus { get; init; } = string.Empty;
    public string LastPeerFailure { get; init; } = string.Empty;
    public long DiscoveredPeers { get; init; }
    public long TrackerPeersDiscovered { get; init; }
    public long DhtPeersDiscovered { get; init; }
    public long PexPeersDiscovered { get; init; }
    public long LocalPeersDiscovered { get; init; }
    public long OtherPeersDiscovered { get; init; }
    public long ConnectionFailures { get; init; }
    public bool PeerListenerConfigured { get; init; }
    public string DhtState { get; init; } = string.Empty;
    public int DhtNodes { get; init; }
    public int TrackerCount { get; init; }
    public string EngineVersion { get; init; } = string.Empty;
    public bool StreamingAvailable { get; init; }
    public bool HasMetadata { get; init; }
    public DateTimeOffset AddedUtc { get; init; }
}

public sealed class TorrentAddResultR1644
{
    public required string TorrentId { get; init; }
    public required TorrentStatusSnapshotR1644 Snapshot { get; init; }
    public string? StartError { get; init; }
}

public sealed class TorrentPeerSnapshotR1644
{
    public string Endpoint { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string DownloadRate { get; init; } = string.Empty;
    public string UploadRate { get; init; } = string.Empty;
    public string Encryption { get; init; } = string.Empty;
    public string Client { get; init; } = string.Empty;
    public bool IsSeeder { get; init; }
}

public sealed class TorrentTrackerSnapshotR1644
{
    public string Tracker { get; init; } = string.Empty;
    public string Announce { get; init; } = string.Empty;
    public string Scrape { get; init; } = string.Empty;
}

public sealed class TorrentItemR1644 : INotifyPropertyChanged
{
    private string _name = "Torrent";
    private string _status = "Stopped";
    private double _progress;
    private string _sizeText = "—";
    private string _downloadRate = "0 B/s";
    private string _uploadRate = "0 B/s";
    private string _peers = "0";
    private string _seeds = "0";
    private string _eta = "—";
    private string _ratio = "0.00";
    private string _downloaded = "0 B";
    private string _uploaded = "0 B";
    private string _lastError = string.Empty;
    private string _discoveryStatus = string.Empty;
    private string _trackerStatus = string.Empty;
    private string _peerFailure = string.Empty;
    private string _networkHealth = string.Empty;
    private bool _streamingAvailable;
    private bool _hasMetadata;
    private bool _isChecked;
    private int _queuePosition;
    private string _desiredStateR199 = "Running";

    public string Id { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string PersistentSource { get; init; } = string.Empty;
    public string SavePath { get; init; } = string.Empty;
    public bool CreateSubfolderR1651 { get; init; } = true;
    public DateTimeOffset AddedUtc { get; init; }
    public List<TorrentFileChoiceR1644> PersistedFileChoicesR199 { get; } = [];

    public string Name { get => _name; private set => Set(ref _name, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public string SizeText { get => _sizeText; private set => Set(ref _sizeText, value); }
    public string DownloadRate { get => _downloadRate; private set => Set(ref _downloadRate, value); }
    public string UploadRate { get => _uploadRate; private set => Set(ref _uploadRate, value); }
    public string Peers { get => _peers; private set => Set(ref _peers, value); }
    public string Seeds { get => _seeds; private set => Set(ref _seeds, value); }
    public string Eta { get => _eta; private set => Set(ref _eta, value); }
    public string Ratio { get => _ratio; private set => Set(ref _ratio, value); }
    public string Downloaded { get => _downloaded; private set => Set(ref _downloaded, value); }
    public string Uploaded { get => _uploaded; private set => Set(ref _uploaded, value); }
    public string LastError { get => _lastError; private set => Set(ref _lastError, value); }
    public string DiscoveryStatus { get => _discoveryStatus; private set => Set(ref _discoveryStatus, value); }
    public string TrackerStatus { get => _trackerStatus; private set => Set(ref _trackerStatus, value); }
    public string PeerFailure { get => _peerFailure; private set => Set(ref _peerFailure, value); }
    public string NetworkHealth { get => _networkHealth; private set => Set(ref _networkHealth, value); }
    public string StatusToolTip => string.Join(Environment.NewLine, new[] { DiscoveryStatus, NetworkHealth, TrackerStatus, PeerFailure }.Where(text => !string.IsNullOrWhiteSpace(text)));
    public bool HasMetadata { get => _hasMetadata; private set => Set(ref _hasMetadata, value); }
    public bool CanStream => _streamingAvailable;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
    public int QueuePosition { get => _queuePosition; internal set => Set(ref _queuePosition, value); }
    public string DesiredStateR199 { get => _desiredStateR199; internal set => Set(ref _desiredStateR199, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void ApplySnapshot(TorrentStatusSnapshotR1644 snapshot)
    {
        Name = string.IsNullOrWhiteSpace(snapshot.Name) ? Name : snapshot.Name;
        var engineStatus = string.IsNullOrWhiteSpace(snapshot.LastError) ? snapshot.Status : "Error";
        if ((string.Equals(engineStatus, "Finding peers", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(engineStatus, "Connecting peers", StringComparison.OrdinalIgnoreCase)) &&
            (snapshot.LiveTransferActive || snapshot.DownloadRate > 0))
        {
            // Never keep a discovery-only label while TorrentHost is actively receiving data.
            engineStatus = "Downloading";
        }
        Status = string.Equals(DesiredStateR199, "Queued", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(engineStatus, "Stopped", StringComparison.OrdinalIgnoreCase)
            ? "Queued"
            : engineStatus;
        Progress = snapshot.Progress;
        SizeText = snapshot.TotalSize > 0 ? TorrentClientR1644.FormatSizeR1644(snapshot.TotalSize) : "Metadata pending";
        DownloadRate = TorrentClientR1644.FormatRateR1644(snapshot.DownloadRate);
        UploadRate = TorrentClientR1644.FormatRateR1644(snapshot.UploadRate);
        Peers = snapshot.Peers.ToString();
        Seeds = snapshot.Seeds.ToString();
        Eta = snapshot.Progress >= 100 ? "Done" : TorrentClientR1644.FormatEtaR1644(snapshot.EtaSeconds);
        Ratio = snapshot.Ratio.ToString("0.00");
        Downloaded = TorrentClientR1644.FormatSizeR1644(snapshot.Downloaded);
        Uploaded = TorrentClientR1644.FormatSizeR1644(snapshot.Uploaded);
        LastError = snapshot.LastError;
        DiscoveryStatus = snapshot.DiscoveryStatus;
        TrackerStatus = snapshot.LastTrackerStatus;
        PeerFailure = snapshot.LastPeerFailure;
        var listener = snapshot.PeerListenerConfigured ? "listener ready" : "listener unavailable";
        var transferState = snapshot.LiveTransferActive ? "live transfer" : "idle transfer";
        NetworkHealth =
            $"{snapshot.EngineVersion} • {listener} • {transferState} • verified {snapshot.VerifiedProgress:0.0}% • " +
            $"DHT {snapshot.DhtState} ({snapshot.DhtNodes} nodes) • {snapshot.TrackerCount} trackers • " +
            $"found T:{snapshot.TrackerPeersDiscovered} D:{snapshot.DhtPeersDiscovered} " +
            $"P:{snapshot.PexPeersDiscovered} L:{snapshot.LocalPeersDiscovered} O:{snapshot.OtherPeersDiscovered} • " +
            $"connect failures {snapshot.ConnectionFailures}";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusToolTip)));
        HasMetadata = snapshot.HasMetadata;
        _streamingAvailable = snapshot.StreamingAvailable;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStream)));
    }

    internal void MarkOperationError(string message)
    {
        LastError = message;
        Status = "Error";
    }

    internal void ClearOperationError()
    {
        LastError = string.Empty;
    }

    internal void MarkQueuedR199()
    {
        DesiredStateR199 = "Queued";
        Status = "Queued";
    }

    internal void SetDesiredStateR199(string state)
    {
        DesiredStateR199 = state;
        if (string.Equals(state, "Queued", StringComparison.OrdinalIgnoreCase))
        {
            Status = "Queued";
        }
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class TorrentFileChoiceR1644 : INotifyPropertyChanged
{
    private bool _selected;
    private TorrentPriorityR1644 _priority;

    public int Index { get; init; }
    public string Path { get; init; } = string.Empty;
    public long Length { get; init; }
    public double Progress { get; init; }
    public string SizeText => TorrentClientR1644.FormatSizeR1644(Length);
    public string ProgressText => $"{Progress:0.0}%";

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public TorrentPriorityR1644 Priority
    {
        get => _priority;
        set
        {
            if (_priority == value)
            {
                return;
            }
            _priority = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Priority)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TorrentClientR1644 : IAsyncDisposable
{
    private sealed class HostResponse
    {
        public string RequestId { get; init; } = string.Empty;
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public JsonElement Data { get; init; }
    }

    private sealed class HostPreview
    {
        public string PreviewId { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string PersistentSource { get; init; } = string.Empty;
        public string Name { get; init; } = "Torrent";
        public long TotalSize { get; init; }
        public bool IsMagnet { get; init; }
        public HostFile[] Files { get; init; } = Array.Empty<HostFile>();
    }

    private sealed class HostFile
    {
        public int Index { get; init; }
        public string Path { get; init; } = string.Empty;
        public long Length { get; init; }
        public double Progress { get; init; }
        public bool Selected { get; init; }
        public string Priority { get; init; } = "Normal";
    }

    private sealed class HostAddResult
    {
        public string TorrentId { get; init; } = string.Empty;
        public string? StartError { get; init; }
        public TorrentStatusSnapshotR1644 Snapshot { get; init; } = new();
    }

    private sealed class HostPeer
    {
        public string Endpoint { get; init; } = string.Empty;
        public string Direction { get; init; } = string.Empty;
        public long DownloadRate { get; init; }
        public long UploadRate { get; init; }
        public string Encryption { get; init; } = string.Empty;
        public string Client { get; init; } = string.Empty;
        public bool IsSeeder { get; init; }
    }

    private sealed class HostStream
    {
        public string Url { get; init; } = string.Empty;
    }

    private const string HostProtocolPrefixR196 = "MDTH1 ";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly object _stderrTailLockR196 = new();
    private readonly Queue<string> _stderrTailR196 = new();
    private Process? _process;
    private int _disposeGate;

    // MEDIADOCK_TORRENT_ISOLATED_HOST_R190
    public static string TorrentHostRelativePathR190 => Path.Combine("TorrentHost", "MediaDock.TorrentHost.exe");

    public static bool IsTorrentSourceR1644(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var value = source.Trim();
        if (value.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            return value.Contains("xt=urn:", StringComparison.OrdinalIgnoreCase);
        }

        if (File.Exists(value))
        {
            return Path.GetExtension(value).Equals(".torrent", StringComparison.OrdinalIgnoreCase);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               uri.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<TorrentPreviewR1644> PrepareAsync(string source, CancellationToken token)
    {
        HostPreview preview;
        try
        {
            preview = await SendAsync<HostPreview>(
                "prepare",
                new { Source = source },
                TimeSpan.FromSeconds(40),
                token);
        }
        catch (Exception ex) when (IsHostCommunicationFailureR196(ex) && !token.IsCancellationRequested)
        {
            // Opening metadata is idempotent. If the isolated host died during startup/IPC,
            // give it one clean restart and retry automatically instead of making the user
            // reopen the same .torrent file.
            await Task.Delay(300, token);
            preview = await SendAsync<HostPreview>(
                "prepare",
                new { Source = source },
                TimeSpan.FromSeconds(40),
                token);
        }

        return new TorrentPreviewR1644
        {
            PreviewId = preview.PreviewId,
            Source = preview.Source,
            PersistentSource = string.IsNullOrWhiteSpace(preview.PersistentSource) ? preview.Source : preview.PersistentSource,
            Name = preview.Name,
            TotalSize = preview.TotalSize,
            IsMagnet = preview.IsMagnet,
            Files = preview.Files.Select(file => new TorrentPreviewFileR1644
            {
                Index = file.Index,
                Path = file.Path,
                Length = file.Length,
                Selected = file.Selected,
                Priority = ParsePriority(file.Priority)
            }).ToArray()
        };
    }

    internal static bool IsHostCommunicationFailureR196(Exception ex)
    {
        return ex is TimeoutException ||
               (ex is InvalidOperationException &&
                (ex.Message.Contains("TorrentHost could not complete its startup handshake", StringComparison.OrdinalIgnoreCase) ||
                 ex.Message.Contains("Torrent engine communication was interrupted", StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<TorrentAddResultR1644> AddPreparedAsync(
        TorrentPreviewR1644 preview,
        string savePath,
        bool streaming,
        bool startImmediately,
        bool createSubfolder,
        CancellationToken token)
    {
        var result = await SendAsync<HostAddResult>(
            "add",
            new
            {
                preview.PreviewId,
                SavePath = savePath,
                StartImmediately = startImmediately,
                EnableStreaming = streaming,
                CreateSubfolder = createSubfolder,
                Files = preview.Files.Select(file => new
                {
                    file.Index,
                    file.Selected,
                    Priority = file.Priority.ToString()
                }).ToArray()
            },
            TimeSpan.FromSeconds(40),
            token);

        return new TorrentAddResultR1644
        {
            TorrentId = result.TorrentId,
            Snapshot = result.Snapshot,
            StartError = result.StartError
        };
    }

    public async Task DiscardPreparedAsync(TorrentPreviewR1644 preview)
        => _ = await SendAsync<JsonElement>(
            "discardprepared",
            new { preview.PreviewId },
            TimeSpan.FromSeconds(8),
            CancellationToken.None);

    public Task<IReadOnlyList<TorrentStatusSnapshotR1644>> GetStatusAsync(CancellationToken token = default)
        => SendListAsync<TorrentStatusSnapshotR1644>("status", new { }, TimeSpan.FromSeconds(8), token);

    public Task<TorrentStatusSnapshotR1644> StartAsync(string torrentId)
        => SendAsync<TorrentStatusSnapshotR1644>("start", new { TorrentId = torrentId }, TimeSpan.FromSeconds(35), CancellationToken.None);

    public Task<TorrentStatusSnapshotR1644> PauseAsync(string torrentId)
        => SendAsync<TorrentStatusSnapshotR1644>("pause", new { TorrentId = torrentId }, TimeSpan.FromSeconds(15), CancellationToken.None);

    public Task<TorrentStatusSnapshotR1644> StopAsync(string torrentId)
        => SendAsync<TorrentStatusSnapshotR1644>("stop", new { TorrentId = torrentId }, TimeSpan.FromSeconds(20), CancellationToken.None);

    public Task<TorrentStatusSnapshotR1644> RecheckAsync(string torrentId)
        => SendAsync<TorrentStatusSnapshotR1644>("recheck", new { TorrentId = torrentId }, TimeSpan.FromMinutes(16), CancellationToken.None);

    public Task<TorrentStatusSnapshotR1644> AnnounceAsync(string torrentId)
        => SendAsync<TorrentStatusSnapshotR1644>("announce", new { TorrentId = torrentId }, TimeSpan.FromSeconds(30), CancellationToken.None);

    public async Task RemoveAsync(string torrentId, bool deleteData = false)
        => _ = await SendAsync<JsonElement>("remove", new { TorrentId = torrentId, DeleteData = deleteData }, TimeSpan.FromSeconds(30), CancellationToken.None);

    public async Task<IReadOnlyList<TorrentFileChoiceR1644>> GetFilesAsync(string torrentId)
    {
        var files = await SendListAsync<HostFile>("files", new { TorrentId = torrentId }, TimeSpan.FromSeconds(10), CancellationToken.None);
        return files.Select(file => new TorrentFileChoiceR1644
        {
            Index = file.Index,
            Path = file.Path,
            Length = file.Length,
            Progress = file.Progress,
            Selected = file.Selected,
            Priority = ParsePriority(file.Priority)
        }).ToArray();
    }

    public async Task ApplyFileChoicesAsync(string torrentId, IEnumerable<TorrentFileChoiceR1644> choices)
    {
        var files = choices.Select(choice => new
        {
            choice.Index,
            choice.Selected,
            Priority = choice.Priority.ToString()
        }).ToArray();
        _ = await SendAsync<JsonElement>("setfiles", new { TorrentId = torrentId, Files = files }, TimeSpan.FromSeconds(20), CancellationToken.None);
    }

    public async Task<IReadOnlyList<TorrentPeerSnapshotR1644>> GetPeersAsync(string torrentId)
    {
        var peers = await SendListAsync<HostPeer>("peers", new { TorrentId = torrentId }, TimeSpan.FromSeconds(10), CancellationToken.None);
        return peers.Select(peer => new TorrentPeerSnapshotR1644
        {
            Endpoint = peer.Endpoint,
            Direction = peer.Direction,
            DownloadRate = FormatRateR1644(peer.DownloadRate),
            UploadRate = FormatRateR1644(peer.UploadRate),
            Encryption = peer.Encryption,
            Client = peer.Client,
            IsSeeder = peer.IsSeeder
        }).ToArray();
    }

    public Task<IReadOnlyList<TorrentTrackerSnapshotR1644>> GetTrackersAsync(string torrentId)
        => SendListAsync<TorrentTrackerSnapshotR1644>("trackers", new { TorrentId = torrentId }, TimeSpan.FromSeconds(10), CancellationToken.None);

    public async Task<string?> CreateStreamingUrlAsync(string torrentId, CancellationToken token)
    {
        var stream = await SendAsync<HostStream>("stream", new { TorrentId = torrentId }, TimeSpan.FromSeconds(90), token);
        return string.IsNullOrWhiteSpace(stream.Url) ? null : stream.Url;
    }

    private async Task<IReadOnlyList<T>> SendListAsync<T>(
        string command,
        object payload,
        TimeSpan timeout,
        CancellationToken token)
    {
        var array = await SendAsync<T[]>(command, payload, timeout, token);
        return array;
    }

    private async Task<T> SendAsync<T>(
        string command,
        object payload,
        TimeSpan timeout,
        CancellationToken token)
    {
        ThrowIfDisposed();
        await _requestGate.WaitAsync(token);
        try
        {
            var process = await EnsureHostProcessR196Async(token);
            var requestId = Guid.NewGuid().ToString("N");
            var request = JsonSerializer.Serialize(new
            {
                RequestId = requestId,
                Command = command,
                Payload = payload
            }, JsonOptions);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutSource.CancelAfter(timeout);

            try
            {
                await process.StandardInput.WriteLineAsync(request).WaitAsync(timeoutSource.Token);
                await process.StandardInput.FlushAsync().WaitAsync(timeoutSource.Token);
                var responsePayload = await ReadFramedHostPayloadR196Async(
                    process,
                    timeoutSource.Token,
                    command);

                var response = JsonSerializer.Deserialize<HostResponse>(responsePayload, JsonOptions)
                    ?? throw new InvalidDataException("The isolated torrent engine returned an empty response.");
                if (!string.Equals(response.RequestId, requestId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The isolated torrent engine response did not match the request.");
                }
                if (!response.Ok)
                {
                    throw new InvalidOperationException(response.Error ?? "The isolated torrent engine rejected the operation.");
                }

                if (typeof(T) == typeof(JsonElement))
                {
                    return (T)(object)response.Data;
                }

                var value = response.Data.Deserialize<T>(JsonOptions);
                return value ?? throw new InvalidDataException("The isolated torrent engine returned incomplete data.");
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                RestartHostAfterFaultR190();
                throw new TimeoutException($"Torrent engine command '{command}' timed out. The torrent engine was restarted; MediaDock stayed open.");
            }
            catch (Exception ex) when (ex is IOException or EndOfStreamException or InvalidDataException or JsonException)
            {
                var diagnostic = DescribeHostExitR196(process);
                RestartHostAfterFaultR190();
                throw new InvalidOperationException(
                    "Torrent engine communication was interrupted, but MediaDock stayed open. " +
                    "The isolated engine was reset safely. " + diagnostic +
                    " See %LOCALAPPDATA%\\AJCoder\\MediaDock\\Logs\\TorrentHost-Stderr.log for details.",
                    ex);
            }
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private static bool TryExtractHostProtocolPayloadR196(string line, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrEmpty(line) || !line.StartsWith(HostProtocolPrefixR196, StringComparison.Ordinal))
        {
            return false;
        }

        payload = line.Substring(HostProtocolPrefixR196.Length);
        return !string.IsNullOrWhiteSpace(payload);
    }

    private async Task<Process> EnsureHostProcessR196Async(CancellationToken token)
    {
        if (_process is { } existing)
        {
            try
            {
                if (!existing.HasExited)
                {
                    return existing;
                }
            }
            catch
            {
            }

            RestartHostAfterFaultR190();
        }

        var hostPath = Path.Combine(AppContext.BaseDirectory, TorrentHostRelativePathR190);
        if (!File.Exists(hostPath))
        {
            throw new FileNotFoundException(
                "The isolated MediaDock torrent engine is missing. Reinstall this MediaDock test build.",
                hostPath);
        }

        var startInfo = new ProcessStartInfo(hostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            WorkingDirectory = AppContext.BaseDirectory
        };

        // R1.9.10: stdin EOF is the TorrentHost lifetime guard. Do not bind the host to a
        // parent PID because a transient Process lookup/event can terminate a healthy host.
        lock (_stderrTailLockR196)
        {
            _stderrTailR196.Clear();
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("MediaDock could not start the isolated torrent engine.");

        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                RecordHostDiagnosticR196(args.Data);
            }
        };
        process.BeginErrorReadLine();
        _process = process;

        try
        {
            using var startupSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            startupSource.CancelAfter(TimeSpan.FromSeconds(12));

            var requestId = Guid.NewGuid().ToString("N");
            var request = JsonSerializer.Serialize(new
            {
                RequestId = requestId,
                Command = "ping",
                Payload = new { }
            }, JsonOptions);

            await process.StandardInput.WriteLineAsync(request).WaitAsync(startupSource.Token);
            await process.StandardInput.FlushAsync().WaitAsync(startupSource.Token);

            var payload = await ReadFramedHostPayloadR196Async(process, startupSource.Token, "startup-ping");
            var response = JsonSerializer.Deserialize<HostResponse>(payload, JsonOptions)
                ?? throw new InvalidDataException("TorrentHost returned an empty startup response.");

            if (!string.Equals(response.RequestId, requestId, StringComparison.Ordinal) || !response.Ok)
            {
                throw new InvalidDataException(
                    response.Error ?? "TorrentHost startup response did not match the request.");
            }

            if (response.Data.ValueKind != JsonValueKind.Object ||
                !response.Data.TryGetProperty("Version", out var versionElement) ||
                !string.Equals(versionElement.GetString(), "R1.6.53", StringComparison.Ordinal))
            {
                throw new InvalidDataException("TorrentHost startup version handshake failed.");
            }

            return process;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            var diagnostic = DescribeHostExitR196(process);
            RestartHostAfterFaultR190();
            throw new TimeoutException(
                "TorrentHost did not complete its startup handshake within 12 seconds. " + diagnostic);
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or InvalidDataException or JsonException)
        {
            var diagnostic = DescribeHostExitR196(process);
            RestartHostAfterFaultR190();
            throw new InvalidOperationException(
                "TorrentHost could not complete its startup handshake. " + diagnostic +
                " See %LOCALAPPDATA%\\AJCoder\\MediaDock\\Logs\\TorrentHost-Stderr.log.",
                ex);
        }
    }

    private async Task<string> ReadFramedHostPayloadR196Async(
        Process process,
        CancellationToken token,
        string command)
    {
        for (var readAttempt = 0; readAttempt < 128; readAttempt++)
        {
            var line = await process.StandardOutput.ReadLineAsync().WaitAsync(token);
            if (line is null)
            {
                throw new EndOfStreamException(
                    $"TorrentHost ended while MediaDock was waiting for '{command}'. {DescribeHostExitR196(process)}");
            }

            if (TryExtractHostProtocolPayloadR196(line, out var framedPayload))
            {
                return framedPayload;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                RecordHostDiagnosticR196("[stdout-noise] " + line);
            }
        }

        throw new InvalidDataException(
            $"TorrentHost produced too much unframed output while handling '{command}'.");
    }

    private void RecordHostDiagnosticR196(string line)
    {
        WriteTorrentHostClientLogR190(line);
        lock (_stderrTailLockR196)
        {
            _stderrTailR196.Enqueue(line);
            while (_stderrTailR196.Count > 12)
            {
                _stderrTailR196.Dequeue();
            }
        }
    }

    private string DescribeHostExitR196(Process process)
    {
        var state = "process state unavailable";
        try
        {
            state = process.HasExited
                ? $"TorrentHost exited with code {process.ExitCode}."
                : "TorrentHost was still running when the protocol failed.";
        }
        catch
        {
        }

        string[] tail;
        lock (_stderrTailLockR196)
        {
            tail = _stderrTailR196.ToArray();
        }

        if (tail.Length == 0)
        {
            return state;
        }

        var last = tail[^1].Trim();
        if (last.Length > 240)
        {
            last = last[..240] + "…";
        }

        return state + " Last engine diagnostic: " + last;
    }

    private void RestartHostAfterFaultR190()
    {
        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void WriteTorrentHostClientLogR190(string line)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AJCoder",
                "MediaDock",
                "Logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "TorrentHost-Stderr.log"),
                $"{DateTimeOffset.Now:O} {line}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeGate) != 0)
        {
            throw new ObjectDisposedException(nameof(TorrentClientR1644));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeGate, 1) != 0)
        {
            return;
        }

        await _requestGate.WaitAsync();
        try
        {
            var process = _process;
            if (process is { HasExited: false })
            {
                try
                {
                    var request = JsonSerializer.Serialize(new
                    {
                        RequestId = Guid.NewGuid().ToString("N"),
                        Command = "shutdown",
                        Payload = new { }
                    }, JsonOptions);
                    await process.StandardInput.WriteLineAsync(request);
                    await process.StandardInput.FlushAsync();
                    process.WaitForExit(3000);
                }
                catch
                {
                }
            }
            RestartHostAfterFaultR190();
        }
        finally
        {
            _requestGate.Release();
            _requestGate.Dispose();
        }
    }

    private static TorrentPriorityR1644 ParsePriority(string? value)
    {
        return Enum.TryParse<TorrentPriorityR1644>(value, true, out var priority)
            ? priority
            : TorrentPriorityR1644.Normal;
    }

    public static string FormatRateR1644(long bytesPerSecond)
    {
        if (bytesPerSecond >= 1024L * 1024L)
        {
            return $"{bytesPerSecond / (1024d * 1024d):0.0} MB/s";
        }
        if (bytesPerSecond >= 1024L)
        {
            return $"{bytesPerSecond / 1024d:0.0} KB/s";
        }
        return $"{Math.Max(0, bytesPerSecond)} B/s";
    }

    public static string FormatSizeR1644(long bytes)
    {
        bytes = Math.Max(0, bytes);
        if (bytes >= 1024L * 1024L * 1024L)
        {
            return $"{bytes / (1024d * 1024d * 1024d):0.00} GB";
        }
        if (bytes >= 1024L * 1024L)
        {
            return $"{bytes / (1024d * 1024d):0.0} MB";
        }
        if (bytes >= 1024L)
        {
            return $"{bytes / 1024d:0.0} KB";
        }
        return $"{bytes} B";
    }

    public static string FormatEtaR1644(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
        {
            return "—";
        }

        var span = TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.FromDays(999).TotalSeconds));
        if (span.TotalDays >= 1)
        {
            return $"{(int)span.TotalDays}d {span.Hours}h";
        }
        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        }
        return $"{Math.Max(0, span.Minutes)}m {span.Seconds}s";
    }

    public static void RunSelfTestR1644()
    {
        if (!IsTorrentSourceR1644("magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567") ||
            !IsTorrentSourceR1644("https://example.com/file.torrent") ||
            IsTorrentSourceR1644("https://example.com/file.zip") ||
            !TorrentHostRelativePathR190.EndsWith("MediaDock.TorrentHost.exe", StringComparison.Ordinal) ||
            TryExtractHostProtocolPayloadR196("• dependency diagnostic", out _) ||
            !TryExtractHostProtocolPayloadR196(HostProtocolPrefixR196 + "{\"Ok\":true}", out var protocolPayload) ||
            !string.Equals(protocolPayload, "{\"Ok\":true}", StringComparison.Ordinal) ||
            !IsHostCommunicationFailureR196(new TimeoutException()) ||
            !IsHostCommunicationFailureR196(new InvalidOperationException("Torrent engine communication was interrupted")))
        {
            throw new InvalidOperationException("R1.9.10 isolated torrent client/protocol contract failed.");
        }

        var hostPath = Path.Combine(AppContext.BaseDirectory, TorrentHostRelativePathR190);
        if (File.Exists(hostPath))
        {
            RunRealClientHostLifecycleSelfTestR196();
        }
    }

    private static void RunRealClientHostLifecycleSelfTestR196()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"MediaDock-TorrentClientSelfTest-{Guid.NewGuid():N}");
        var download = Path.Combine(root, "download");
        var torrentPath = Path.Combine(root, "saved-valid.torrent");

        Directory.CreateDirectory(download);
        try
        {
            File.WriteAllBytes(torrentPath, BuildSavedTorrentSelfTestBytesR196());

            var client = new TorrentClientR1644();
            try
            {
                var preview = client
                    .PrepareAsync(torrentPath, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                if (!string.Equals(preview.Name, "test.bin", StringComparison.Ordinal) ||
                    preview.Files.Count != 1 ||
                    preview.TotalSize != 1)
                {
                    throw new InvalidOperationException(
                        "R1.9.10 WPF TorrentClient saved-file prepare contract failed.");
                }

                var add = client
                    .AddPreparedAsync(
                        preview,
                        download,
                        streaming: false,
                        startImmediately: true,
                        createSubfolder: true,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                if (!string.IsNullOrWhiteSpace(add.StartError) ||
                    string.Equals(add.Snapshot.Status, "Stopped", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(add.Snapshot.Status, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"R1.9.10 WPF TorrentClient automatic-start contract failed. Status={add.Snapshot.Status}, Error={add.StartError}");
                }

                client.StopAsync(add.TorrentId).GetAwaiter().GetResult();
                client.RemoveAsync(add.TorrentId).GetAwaiter().GetResult();
            }
            finally
            {
                client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static byte[] BuildSavedTorrentSelfTestBytesR196()
    {
        var prefix = Encoding.ASCII.GetBytes(
            "d8:announce17:http://127.0.0.1/4:infod6:lengthi1e4:name8:test.bin12:piece lengthi16384e6:pieces20:");
        var suffix = Encoding.ASCII.GetBytes("ee");
        var bytes = new byte[prefix.Length + 20 + suffix.Length];
        Buffer.BlockCopy(prefix, 0, bytes, 0, prefix.Length);
        Buffer.BlockCopy(suffix, 0, bytes, prefix.Length + 20, suffix.Length);
        return bytes;
    }
}
