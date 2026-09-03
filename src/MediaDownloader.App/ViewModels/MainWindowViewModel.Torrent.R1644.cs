using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MediaDownloader.Core.Services;

namespace MediaDownloader.ViewModels;

public sealed partial class MainWindowViewModel
{
    private sealed class TorrentSessionDocumentR199
    {
        public int Version { get; set; } = 4;
        public List<TorrentSessionEntryR199> Torrents { get; set; } = [];
    }

    private sealed class TorrentSessionEntryR199
    {
        public string Source { get; set; } = string.Empty;
        public string PersistentSource { get; set; } = string.Empty;
        public string SavePath { get; set; } = string.Empty;
        public string DesiredState { get; set; } = "Running";
        public bool CreateSubfolder { get; set; } = true;
        public int QueuePosition { get; set; }
        // MEDIADOCK_TORRENT_SELF_CONTAINED_SESSION_R1653
        // A compact copy of the .torrent metadata makes the queue independent of the
        // original file the user browsed to and provides recovery if a cache file is lost.
        public string MetadataBase64 { get; set; } = string.Empty;
        public List<TorrentSessionFileR199> Files { get; set; } = [];
    }

    private sealed class TorrentSessionFileR199
    {
        public int Index { get; set; }
        public bool Selected { get; set; } = true;
        public string Priority { get; set; } = nameof(TorrentPriorityR1644.Normal);
    }

    private static readonly JsonSerializerOptions TorrentJsonR199 = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private TorrentClientR1644? _torrentClientR1644;
    private readonly DispatcherTimer _torrentRefreshTimerR1644 = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly SemaphoreSlim _torrentStateGateR199 = new(1, 1);
    private string _torrentSourceR1644 = string.Empty;
    private string _torrentStatusR1644 = "Open a .torrent or paste a magnet link. Downloads start automatically in Fast mode.";
    private TorrentItemR1644? _selectedTorrentR1644;
    private int _torrentDisposeGateR1644;
    private int _torrentRefreshGateR1644;
    private int _torrentQueueGateR199;
    private int _torrentRestoreGateR199;
    private int _torrentLoadedRestoreGateR1653;
    private readonly List<TorrentSessionEntryR199> _retainedTorrentSessionEntriesR1646 = [];

    public ObservableCollection<TorrentItemR1644> TorrentsR1644 { get; } = [];
    public string TorrentSourceR1644 { get => _torrentSourceR1644; set => SetProperty(ref _torrentSourceR1644, value); }
    public string TorrentStatusR1644 { get => _torrentStatusR1644; private set => SetProperty(ref _torrentStatusR1644, value); }
    public TorrentItemR1644? SelectedTorrentR1644 { get => _selectedTorrentR1644; set => SetProperty(ref _selectedTorrentR1644, value); }
    public TorrentPreferencesR199 TorrentPreferencesR199 { get; private set; } = new();

    private static string TorrentStateRootR199 => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AJCoder", "MediaDock", "TorrentClient");

    private static string TorrentSettingsPathR199 => Path.Combine(TorrentStateRootR199, "settings.json");
    private static string TorrentSessionPathR199 => Path.Combine(TorrentStateRootR199, "session.json");
    private static string TorrentSessionBackupPathR1646 => TorrentSessionPathR199 + ".bak";
    private static string TorrentMetadataRootR1653 => Path.Combine(TorrentStateRootR199, "Metadata");
    private const int MaxEmbeddedTorrentMetadataBytesR1653 = 16 * 1024 * 1024;

    public string TorrentOutputDirectoryR1644 => string.IsNullOrWhiteSpace(TorrentPreferencesR199.DownloadDirectory)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "MediaDock", "Torrents")
        : TorrentPreferencesR199.DownloadDirectory;

    private TorrentClientR1644 GetTorrentClientR1644()
    {
        if (Volatile.Read(ref _torrentDisposeGateR1644) != 0)
        {
            throw new ObjectDisposedException(nameof(TorrentClientR1644));
        }
        return _torrentClientR1644 ??= new TorrentClientR1644();
    }

    private void InitializeTorrentR1644()
    {
        LoadTorrentPreferencesR199();
        try
        {
            Directory.CreateDirectory(TorrentStateRootR199);
            Directory.CreateDirectory(TorrentMetadataRootR1653);
            Directory.CreateDirectory(TorrentOutputDirectoryR1644);
        }
        catch (Exception ex)
        {
            TorrentStatusR1644 = "Torrent folder is not writable.";
            global::MediaDownloader.App.WriteCrashLog("Torrent.OutputDirectory.R199", ex);
        }

        _torrentRefreshTimerR1644.Tick += async (_, _) => await RefreshTorrentTelemetryR190Async();
        _torrentRefreshTimerR1644.Start();
    }

    private void LoadTorrentPreferencesR199()
    {
        try
        {
            Directory.CreateDirectory(TorrentStateRootR199);
            if (File.Exists(TorrentSettingsPathR199))
            {
                TorrentPreferencesR199 = JsonSerializer.Deserialize<TorrentPreferencesR199>(File.ReadAllText(TorrentSettingsPathR199), TorrentJsonR199) ?? new();
            }
            NormalizeTorrentPreferencesR199(TorrentPreferencesR199);
            WriteJsonAtomicR199(TorrentSettingsPathR199, TorrentPreferencesR199);
        }
        catch (Exception ex)
        {
            TorrentPreferencesR199 = new();
            global::MediaDownloader.App.WriteCrashLog("Torrent.Settings.Load.R199", ex);
        }
    }

    private static void NormalizeTorrentPreferencesR199(TorrentPreferencesR199 settings)
    {
        settings.MaximumConnections = Math.Clamp(settings.MaximumConnections, 20, 2000);
        settings.MaximumPeersPerTorrent = Math.Clamp(settings.MaximumPeersPerTorrent, 10, 1000);
        settings.UploadSlotsPerTorrent = Math.Clamp(settings.UploadSlotsPerTorrent, 1, 100);
        settings.MaximumHalfOpenConnections = Math.Clamp(settings.MaximumHalfOpenConnections, 8, 256);
        settings.MaximumActiveDownloads = Math.Clamp(settings.MaximumActiveDownloads, 1, 32);
        settings.PeerRecoveryAttempts = Math.Clamp(settings.PeerRecoveryAttempts, 1, 60);
        settings.PeerRecoveryIntervalSeconds = Math.Clamp(settings.PeerRecoveryIntervalSeconds, 2, 120);
        settings.ListeningPort = Math.Clamp(settings.ListeningPort, 0, 65535);
        settings.DhtPort = Math.Clamp(settings.DhtPort, 0, 65535);
        settings.MaximumDownloadRateKbps = Math.Max(0, settings.MaximumDownloadRateKbps);
        settings.MaximumUploadRateKbps = Math.Max(0, settings.MaximumUploadRateKbps);
        if (string.IsNullOrWhiteSpace(settings.DownloadDirectory))
        {
            settings.DownloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "MediaDock", "Torrents");
        }
    }

    public async Task ApplyTorrentPreferencesR199Async(TorrentPreferencesR199 settings)
    {
        NormalizeTorrentPreferencesR199(settings);
        var currentEntries = BuildTorrentSessionDocumentR1646().Torrents;
        TorrentPreferencesR199 = settings.Clone();
        Directory.CreateDirectory(TorrentOutputDirectoryR1644);
        WriteJsonAtomicR199(TorrentSettingsPathR199, TorrentPreferencesR199);
        if (TorrentPreferencesR199.RememberLoadedTorrents)
        {
            await SaveTorrentSessionR199Async();
        }
        else
        {
            _retainedTorrentSessionEntriesR1646.Clear();
            try
            {
                if (File.Exists(TorrentSessionPathR199)) File.Delete(TorrentSessionPathR199);
                if (File.Exists(TorrentSessionBackupPathR1646)) File.Delete(TorrentSessionBackupPathR1646);
            }
            catch (Exception ex) { global::MediaDownloader.App.WriteCrashLog("Torrent.Session.DisablePersistence.R199", ex); }
        }

        TorrentStatusR1644 = "Applying torrent settings and restarting the isolated engine safely...";
        var old = _torrentClientR1644;
        _torrentClientR1644 = null;
        if (old is not null)
        {
            try { await old.DisposeAsync(); }
            catch (Exception ex) { global::MediaDownloader.App.WriteCrashLog("Torrent.Settings.Restart.R199", ex); }
        }
        TorrentsR1644.Clear();
        SelectedTorrentR1644 = null;
        await RestoreEntriesR199Async(currentEntries, "Torrent settings applied.");
    }

    private async Task RefreshTorrentTelemetryR190Async()
    {
        if (Interlocked.Exchange(ref _torrentRefreshGateR1644, 1) != 0 ||
            Volatile.Read(ref _torrentDisposeGateR1644) != 0 ||
            _torrentClientR1644 is null)
        {
            return;
        }

        try
        {
            var snapshots = await _torrentClientR1644.GetStatusAsync();
            var byId = snapshots.ToDictionary(snapshot => snapshot.Id, StringComparer.Ordinal);
            foreach (var item in TorrentsR1644.ToArray())
            {
                if (byId.TryGetValue(item.Id, out var snapshot))
                {
                    item.ApplySnapshot(snapshot);
                }
                else
                {
                    item.MarkOperationError("Torrent engine session was reset. MediaDock will restore this torrent from its saved session on the next engine restart.");
                }
            }
            UpdateQueuePositionsR199();
            UpdateTorrentLiveStatusR1646();
            await MaintainTorrentQueueR199Async();
        }
        catch (Exception ex)
        {
            global::MediaDownloader.App.WriteCrashLog("Torrent.IsolatedHost.Refresh.R199", ex);
            TorrentStatusR1644 = "Torrent engine telemetry was interrupted. Loaded torrent state is still saved.";
        }
        finally
        {
            Volatile.Write(ref _torrentRefreshGateR1644, 0);
        }
    }

    private void UpdateTorrentLiveStatusR1646()
    {
        var item = SelectedTorrentR1644;
        if (item is null)
        {
            return;
        }

        if (string.Equals(item.Status, "Downloading", StringComparison.OrdinalIgnoreCase))
        {
            // MEDIADOCK_TORRENT_STATUS_SUMMARY_R1647
            TorrentStatusR1644 =
                $"{item.Name}: {item.Progress:0.0}% • ↓ {item.DownloadRate} • ↑ {item.UploadRate} • " +
                $"P:{item.Peers} • S:{item.Seeds} • ETA {item.Eta} • ratio {item.Ratio} • {item.Downloaded} received";
            return;
        }

        if (string.Equals(item.Status, "Finding peers", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Status, "Connecting peers", StringComparison.OrdinalIgnoreCase))
        {
            TorrentStatusR1644 = string.IsNullOrWhiteSpace(item.DiscoveryStatus)
                ? $"{item.Name}: {item.Status}"
                : $"{item.Name}: {item.Status} • {item.DiscoveryStatus}";
            return;
        }

        if (string.Equals(item.Status, "Seeding", StringComparison.OrdinalIgnoreCase) ||
            item.Progress >= 100d)
        {
            TorrentStatusR1644 = $"{item.Name}: complete • ratio {item.Ratio}";
            return;
        }

        if (string.Equals(item.Status, "Error", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(item.LastError))
        {
            TorrentStatusR1644 = $"{item.Name}: {item.LastError}";
            return;
        }

        TorrentStatusR1644 = $"{item.Name}: {item.Status} • {item.Progress:0.0}%";
    }

    public async Task<TorrentPreviewR1644> PrepareTorrentR1644Async(string source)
    {
        if (!TorrentClientR1644.IsTorrentSourceR1644(source))
        {
            throw new InvalidOperationException("Enter a magnet link or choose a .torrent file.");
        }

        TorrentStatusR1644 = "Reading torrent metadata in the isolated torrent engine...";
        try
        {
            var preview = await GetTorrentClientR1644().PrepareAsync(source, CancellationToken.None);
            TorrentStatusR1644 = "Torrent metadata is ready.";
            return preview;
        }
        catch (Exception ex)
        {
            if (TorrentClientR1644.IsHostCommunicationFailureR196(ex))
            {
                await ResetTorrentClientAfterCommunicationFailureR196Async();
                TorrentStatusR1644 = "Torrent engine communication was reset safely. Saved torrent state was kept.";
            }
            else
            {
                TorrentStatusR1644 = "Torrent metadata was rejected. MediaDock remains ready.";
            }
            throw;
        }
    }

    private async Task ResetTorrentClientAfterCommunicationFailureR196Async()
    {
        var failedClient = _torrentClientR1644;
        _torrentClientR1644 = null;
        if (failedClient is not null)
        {
            try { await failedClient.DisposeAsync(); }
            catch (Exception ex) { global::MediaDownloader.App.WriteCrashLog("Torrent.IsolatedHost.Reset.R199", ex); }
        }
        await SaveTorrentSessionR199Async();
    }

    public async Task<TorrentItemR1644> AddPreparedTorrentR1644Async(
        TorrentPreviewR1644 preview,
        string savePath,
        bool streaming,
        bool startImmediately,
        bool createSubfolder)
        => await AddPreparedTorrentInternalR199Async(preview, savePath, streaming, startImmediately, createSubfolder, persist: true, desiredState: null, persistedFiles: null);

    public async Task DiscardPreparedTorrentR1651Async(TorrentPreviewR1644 preview)
    {
        try { await GetTorrentClientR1644().DiscardPreparedAsync(preview); }
        catch (Exception ex) { global::MediaDownloader.App.WriteCrashLog("Torrent.Prepared.Discard.R1651", ex); }
    }

    public void SetTorrentStatusR1651(string status) => TorrentStatusR1644 = status;

    // MEDIADOCK_TORRENT_RESTORE_AFTER_WINDOW_LOADED_R1653
    // Restore only after the WPF window is loaded. This avoids racing app construction,
    // theme initialization and TorrentHost startup. The one-shot gate prevents duplicate rows.
    public async Task RestorePersistedTorrentsAfterLoadedR1653Async()
    {
        if (Interlocked.Exchange(ref _torrentLoadedRestoreGateR1653, 1) != 0) return;
        await Task.Delay(250);
        await RestorePersistedTorrentsR199Async();
    }

    private async Task<TorrentItemR1644> AddPreparedTorrentInternalR199Async(
        TorrentPreviewR1644 preview,
        string savePath,
        bool streaming,
        bool startImmediately,
        bool createSubfolder,
        bool persist,
        string? desiredState,
        IReadOnlyList<TorrentSessionFileR199>? persistedFiles)
    {
        var normalizedSource = NormalizeTorrentSourceR1644(preview.PersistentSource.Length > 0 ? preview.PersistentSource : preview.Source);
        if (TorrentsR1644.Any(item => string.Equals(
                NormalizeTorrentSourceR1644(item.PersistentSource.Length > 0 ? item.PersistentSource : item.Source),
                normalizedSource,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("This torrent is already in the MediaDock Torrent list.");
        }

        var targetFolder = string.IsNullOrWhiteSpace(savePath) ? TorrentOutputDirectoryR1644 : savePath.Trim();
        Directory.CreateDirectory(targetFolder);

        // Explicit dialog choice controls this torrent. AutoStartDownloads controls only
        // the dialog's default checkbox; it must not override a user's direct choice.
        var allowStart = startImmediately && CountActiveDownloadsR199() < TorrentPreferencesR199.MaximumActiveDownloads;
        var initialDesiredState = desiredState ?? (allowStart ? "Running" : startImmediately ? "Queued" : "Stopped");
        TorrentStatusR1644 = allowStart
            ? "Adding torrent and starting Fast download..."
            : string.Equals(initialDesiredState, "Queued", StringComparison.OrdinalIgnoreCase)
                ? "Adding torrent to the queue..."
                : "Adding torrent in stopped state...";

        var result = await GetTorrentClientR1644().AddPreparedAsync(preview, targetFolder, streaming, allowStart, createSubfolder, CancellationToken.None);
        var snapshot = result.Snapshot;
        var item = new TorrentItemR1644
        {
            Id = result.TorrentId,
            Source = preview.Source,
            PersistentSource = string.IsNullOrWhiteSpace(preview.PersistentSource) ? preview.Source : preview.PersistentSource,
            SavePath = targetFolder,
            CreateSubfolderR1651 = createSubfolder,
            AddedUtc = snapshot.AddedUtc
        };
        item.SetDesiredStateR199(initialDesiredState);
        item.ApplySnapshot(snapshot);

        // Initial file selections from the Add New Torrent dialog must survive restart.
        if (preview.Files.Count > 0)
        {
            CacheFileChoicesR199(item, preview.Files.Select(file => new TorrentFileChoiceR1644
            {
                Index = file.Index,
                Path = file.Path,
                Length = file.Length,
                Selected = file.Selected,
                Priority = file.Priority
            }));
        }

        if (persistedFiles is not null && persistedFiles.Count > 0)
        {
            var choices = persistedFiles.Select(file => new TorrentFileChoiceR1644
            {
                Index = file.Index,
                Selected = file.Selected,
                Priority = Enum.TryParse<TorrentPriorityR1644>(file.Priority, true, out var priority) ? priority : TorrentPriorityR1644.Normal
            }).ToList();
            await GetTorrentClientR1644().ApplyFileChoicesAsync(item.Id, choices);
            CacheFileChoicesR199(item, choices);
        }

        if (!string.IsNullOrWhiteSpace(result.StartError))
        {
            item.MarkOperationError(result.StartError);
            TorrentStatusR1644 = "Torrent was added but could not start. Check Details > Trackers/Peers.";
        }
        else if (!allowStart && string.Equals(item.DesiredStateR199, "Queued", StringComparison.OrdinalIgnoreCase))
        {
            item.MarkQueuedR199();
            TorrentStatusR1644 = "Torrent queued. Higher-priority torrents download first.";
        }
        else
        {
            TorrentStatusR1644 = allowStart ? "Torrent started. Discovering peers..." : "Torrent loaded in stopped state.";
        }

        TorrentsR1644.Add(item);
        SelectedTorrentR1644 = item;
        UpdateQueuePositionsR199();

        // MEDIADOCK_FIRST_LOAD_START_CONFIRM_R1651
        // The row exists before the confirmation start. This makes a fresh add follow the
        // same explicit, idempotent Start command used by restored torrents and eliminates
        // the first-load race where the transfer only became live after reopening MediaDock.
        if (allowStart && string.IsNullOrWhiteSpace(result.StartError) &&
            (string.Equals(snapshot.Status, "Stopped", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(snapshot.Status, "Paused", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(snapshot.Status, "Queued", StringComparison.OrdinalIgnoreCase)))
        {
            await Task.Delay(250);
            try
            {
                item.ApplySnapshot(await GetTorrentClientR1644().StartAsync(item.Id));
            }
            catch (Exception ex)
            {
                global::MediaDownloader.App.WriteCrashLog("Torrent.FirstLoadStartConfirm.R1651", ex);
                item.MarkOperationError("Torrent was added, but the immediate start confirmation failed: " + ex.Message);
            }
        }
        var restoredIdentity = NormalizeTorrentSourceR1644(item.PersistentSource.Length > 0 ? item.PersistentSource : item.Source);
        _retainedTorrentSessionEntriesR1646.RemoveAll(entry =>
        {
            var identity = SessionIdentityR1646(entry);
            return !string.IsNullOrWhiteSpace(identity) &&
                   string.Equals(NormalizeTorrentSourceR1644(identity), restoredIdentity, StringComparison.OrdinalIgnoreCase);
        });
        if (persist) await SaveTorrentSessionR199Async();
        return item;
    }

    public async Task StartTorrentR1644Async(TorrentItemR1644 item)
    {
        item.ClearOperationError();
        item.SetDesiredStateR199("Running");
        TorrentStatusR1644 = $"Starting {item.Name}...";
        item.ApplySnapshot(await GetTorrentClientR1644().StartAsync(item.Id));
        TorrentStatusR1644 = "Torrent started. Discovering peers...";
        await SaveTorrentSessionR199Async();
    }

    public async Task PauseTorrentR1644Async(TorrentItemR1644 item)
    {
        item.SetDesiredStateR199("Paused");
        item.ApplySnapshot(await GetTorrentClientR1644().PauseAsync(item.Id));
        TorrentStatusR1644 = "Torrent paused.";
        await SaveTorrentSessionR199Async();
        await MaintainTorrentQueueR199Async();
    }

    public async Task StopTorrentR1644Async(TorrentItemR1644 item)
    {
        item.SetDesiredStateR199("Stopped");
        item.ApplySnapshot(await GetTorrentClientR1644().StopAsync(item.Id));
        TorrentStatusR1644 = "Torrent stopped.";
        await SaveTorrentSessionR199Async();
        await MaintainTorrentQueueR199Async();
    }

    public async Task RecheckTorrentR1644Async(TorrentItemR1644 item)
    {
        TorrentStatusR1644 = $"Rechecking {item.Name}...";
        item.ApplySnapshot(await GetTorrentClientR1644().RecheckAsync(item.Id));
        TorrentStatusR1644 = "Torrent recheck completed.";
        await SaveTorrentSessionR199Async();
    }

    public async Task UpdateTorrentTrackersR199Async(TorrentItemR1644 item)
    {
        TorrentStatusR1644 = $"Updating trackers for {item.Name}...";
        item.ApplySnapshot(await GetTorrentClientR1644().AnnounceAsync(item.Id));
        TorrentStatusR1644 = "Tracker/DHT peer discovery refresh requested.";
    }

    public async Task RemoveTorrentR1644Async(TorrentItemR1644 item, bool deleteData = false)
    {
        try
        {
            await GetTorrentClientR1644().RemoveAsync(item.Id, deleteData);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no longer active", StringComparison.OrdinalIgnoreCase))
        {
            // Row removal is still safe after an isolated worker restart.
        }
        TorrentsR1644.Remove(item);
        if (ReferenceEquals(SelectedTorrentR1644, item)) SelectedTorrentR1644 = TorrentsR1644.FirstOrDefault();
        UpdateQueuePositionsR199();
        TorrentStatusR1644 = deleteData ? "Torrent and downloaded data removed." : "Torrent removed from MediaDock. Downloaded files were kept.";
        await SaveTorrentSessionR199Async();
        await MaintainTorrentQueueR199Async();
    }

    public async Task RemoveCheckedTorrentsR199Async(bool deleteData)
    {
        foreach (var item in TorrentsR1644.Where(x => x.IsChecked).ToArray()) await RemoveTorrentR1644Async(item, deleteData);
    }

    public async Task RemoveAllTorrentsR199Async(bool deleteData)
    {
        foreach (var item in TorrentsR1644.ToArray()) await RemoveTorrentR1644Async(item, deleteData);
    }

    public void SetAllTorrentChecksR199(bool value)
    {
        foreach (var item in TorrentsR1644) item.IsChecked = value;
    }

    public async Task MoveTorrentUpR199Async(TorrentItemR1644 item)
    {
        var index = TorrentsR1644.IndexOf(item);
        if (index <= 0) return;
        TorrentsR1644.Move(index, index - 1);
        UpdateQueuePositionsR199();
        await SaveTorrentSessionR199Async();
        await MaintainTorrentQueueR199Async();
        TorrentStatusR1644 = "Torrent priority moved up.";
    }

    public async Task MoveTorrentDownR199Async(TorrentItemR1644 item)
    {
        var index = TorrentsR1644.IndexOf(item);
        if (index < 0 || index >= TorrentsR1644.Count - 1) return;
        TorrentsR1644.Move(index, index + 1);
        UpdateQueuePositionsR199();
        await SaveTorrentSessionR199Async();
        await MaintainTorrentQueueR199Async();
        TorrentStatusR1644 = "Torrent priority moved down.";
    }

    private int CountActiveDownloadsR199() => TorrentsR1644.Count(item =>
        string.Equals(item.DesiredStateR199, "Running", StringComparison.OrdinalIgnoreCase) &&
        item.Progress < 100 &&
        !string.Equals(item.Status, "Stopped", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(item.Status, "Paused", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(item.Status, "Error", StringComparison.OrdinalIgnoreCase));

    private async Task MaintainTorrentQueueR199Async()
    {
        if (Interlocked.Exchange(ref _torrentQueueGateR199, 1) != 0 || _torrentClientR1644 is null) return;
        try
        {
            var active = CountActiveDownloadsR199();
            var queueChanged = false;
            foreach (var item in TorrentsR1644.Where(x => string.Equals(x.DesiredStateR199, "Queued", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                if (active >= TorrentPreferencesR199.MaximumActiveDownloads) break;
                try
                {
                    item.SetDesiredStateR199("Running");
                    item.ApplySnapshot(await GetTorrentClientR1644().StartAsync(item.Id));
                    active++;
                    queueChanged = true;
                }
                catch (Exception ex)
                {
                    item.MarkOperationError(ex.Message);
                    queueChanged = true;
                }
            }
            if (queueChanged)
            {
                await SaveTorrentSessionR199Async();
            }
        }
        finally { Volatile.Write(ref _torrentQueueGateR199, 0); }
    }

    private void UpdateQueuePositionsR199()
    {
        for (var i = 0; i < TorrentsR1644.Count; i++) TorrentsR1644[i].QueuePosition = i + 1;
    }

    public Task<IReadOnlyList<TorrentFileChoiceR1644>> GetTorrentFilesR1644Async(TorrentItemR1644 item)
        => GetTorrentClientR1644().GetFilesAsync(item.Id);

    public async Task ApplyTorrentFileChoicesR1644Async(TorrentItemR1644 item, IEnumerable<TorrentFileChoiceR1644> choices)
    {
        var list = choices.ToList();
        await GetTorrentClientR1644().ApplyFileChoicesAsync(item.Id, list);
        CacheFileChoicesR199(item, list);
        TorrentStatusR1644 = "Torrent file priorities updated.";
        await SaveTorrentSessionR199Async();
    }

    private static void CacheFileChoicesR199(TorrentItemR1644 item, IEnumerable<TorrentFileChoiceR1644> choices)
    {
        item.PersistedFileChoicesR199.Clear();
        foreach (var file in choices)
        {
            item.PersistedFileChoicesR199.Add(new TorrentFileChoiceR1644 { Index = file.Index, Selected = file.Selected, Priority = file.Priority });
        }
    }

    public Task<IReadOnlyList<TorrentPeerSnapshotR1644>> GetTorrentPeersR1644Async(TorrentItemR1644 item)
        => GetTorrentClientR1644().GetPeersAsync(item.Id);

    public Task<IReadOnlyList<TorrentTrackerSnapshotR1644>> GetTorrentTrackersR1644Async(TorrentItemR1644 item)
        => GetTorrentClientR1644().GetTrackersAsync(item.Id);

    public async Task<string?> CreateTorrentStreamUrlR1644Async(TorrentItemR1644 item)
    {
        TorrentStatusR1644 = "Buffering selected torrent media...";
        var url = await GetTorrentClientR1644().CreateStreamingUrlAsync(item.Id, CancellationToken.None);
        TorrentStatusR1644 = string.IsNullOrWhiteSpace(url) ? "No streamable file found." : "Torrent stream ready.";
        return url;
    }

    private static string NormalizeTorrentSourceR1644(string source)
    {
        if (File.Exists(source)) return Path.GetFullPath(source);
        return source.Trim();
    }

    // MEDIADOCK_TORRENT_SELF_CONTAINED_SESSION_R1653
    private static string CanonicalizeTorrentMetadataBytesR1653(byte[] metadata)
    {
        if (metadata.Length < 8 || metadata.Length > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Saved torrent metadata size is invalid.");
        }

        Directory.CreateDirectory(TorrentMetadataRootR1653);
        var digest = Convert.ToHexString(SHA256.HashData(metadata)).ToLowerInvariant();
        var canonical = Path.Combine(TorrentMetadataRootR1653, digest + ".torrent");
        if (!File.Exists(canonical))
        {
            var temp = canonical + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temp, metadata);
                File.Move(temp, canonical, false);
            }
            catch (IOException) when (File.Exists(canonical))
            {
                // Another restore/add won the same content-addressed write.
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }
        return canonical;
    }

    private static string? TryCanonicalizeTorrentMetadataFileR1653(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        var value = candidate.Trim();
        if (!File.Exists(value) ||
            !Path.GetExtension(value).Equals(".torrent", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return CanonicalizeTorrentMetadataBytesR1653(File.ReadAllBytes(value));
        }
        catch (Exception ex)
        {
            global::MediaDownloader.App.WriteCrashLog("Torrent.Session.MetadataCanonicalize.R1653", ex);
            return value;
        }
    }

    private static string TryReadTorrentMetadataBase64R1653(string persistentSource, string source)
    {
        foreach (var candidate in new[] { persistentSource, source })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var value = candidate.Trim();
            if (!File.Exists(value) ||
                !Path.GetExtension(value).Equals(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(value);
                if (info.Length < 8 || info.Length > MaxEmbeddedTorrentMetadataBytesR1653) continue;
                return Convert.ToBase64String(File.ReadAllBytes(value));
            }
            catch (Exception ex)
            {
                global::MediaDownloader.App.WriteCrashLog("Torrent.Session.MetadataEmbed.R1653", ex);
            }
        }
        return string.Empty;
    }

    private List<TorrentSessionEntryR199> BuildSessionEntriesR199()
    {
        UpdateQueuePositionsR199();
        return TorrentsR1644.Select(item => new TorrentSessionEntryR199
        {
            Source = item.Source,
            PersistentSource = item.PersistentSource,
            SavePath = item.SavePath,
            DesiredState = item.DesiredStateR199,
            CreateSubfolder = item.CreateSubfolderR1651,
            QueuePosition = item.QueuePosition,
            MetadataBase64 = TryReadTorrentMetadataBase64R1653(item.PersistentSource, item.Source),
            Files = item.PersistedFileChoicesR199.Select(file => new TorrentSessionFileR199
            {
                Index = file.Index,
                Selected = file.Selected,
                Priority = file.Priority.ToString()
            }).ToList()
        }).ToList();
    }

    private TorrentSessionDocumentR199 BuildTorrentSessionDocumentR1646()
    {
        var merged = BuildSessionEntriesR199();
        foreach (var entry in _retainedTorrentSessionEntriesR1646.OrderBy(x => x.QueuePosition))
        {
            var key = SessionIdentityR1646(entry);
            if (merged.Any(existing => string.Equals(SessionIdentityR1646(existing), key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            merged.Add(entry);
        }

        return new TorrentSessionDocumentR199
        {
            Version = 4,
            Torrents = merged.OrderBy(x => x.QueuePosition).ToList()
        };
    }

    // MEDIADOCK_TORRENT_PERSISTENCE_V2_R1646
    // Session persistence is eager while MediaDock is running and synchronous during
    // Window.Closing. A failed startup restore must never erase an unresolved torrent entry.
    private async Task SaveTorrentSessionR199Async()
    {
        if (!TorrentPreferencesR199.RememberLoadedTorrents) return;
        await _torrentStateGateR199.WaitAsync();
        try
        {
            Directory.CreateDirectory(TorrentStateRootR199);
            WriteTorrentSessionAtomicR1646(BuildTorrentSessionDocumentR1646());
        }
        catch (Exception ex) { global::MediaDownloader.App.WriteCrashLog("Torrent.Session.Save.R1646", ex); }
        finally { _torrentStateGateR199.Release(); }
    }

    public void PersistTorrentSessionOnClosingR1646()
    {
        if (!TorrentPreferencesR199.RememberLoadedTorrents || Volatile.Read(ref _torrentDisposeGateR1644) != 0) return;

        var entered = false;
        try
        {
            entered = _torrentStateGateR199.Wait(TimeSpan.FromSeconds(3));
            if (!entered)
            {
                global::MediaDownloader.App.WriteCrashLog(
                    "Torrent.Session.Closing.R1646",
                    new TimeoutException("Timed out waiting to persist the torrent session during Window.Closing."));
                return;
            }

            Directory.CreateDirectory(TorrentStateRootR199);
            WriteTorrentSessionAtomicR1646(BuildTorrentSessionDocumentR1646());
        }
        catch (Exception ex)
        {
            global::MediaDownloader.App.WriteCrashLog("Torrent.Session.Closing.R1646", ex);
        }
        finally
        {
            if (entered) _torrentStateGateR199.Release();
        }
    }

    private async Task<TorrentSessionDocumentR199?> ReadTorrentSessionWithRecoveryR1646Async()
    {
        foreach (var candidate in new[] { TorrentSessionPathR199, TorrentSessionBackupPathR1646 })
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                var document = JsonSerializer.Deserialize<TorrentSessionDocumentR199>(
                    await File.ReadAllTextAsync(candidate),
                    TorrentJsonR199);
                if (document is not null)
                {
                    document.Torrents ??= [];
                    return document;
                }
            }
            catch (Exception ex)
            {
                global::MediaDownloader.App.WriteCrashLog(
                    string.Equals(candidate, TorrentSessionPathR199, StringComparison.OrdinalIgnoreCase)
                        ? "Torrent.Session.ReadPrimary.R1646"
                        : "Torrent.Session.ReadBackup.R1646",
                    ex);
            }
        }

        return null;
    }

    private async Task RestorePersistedTorrentsR199Async()
    {
        if (!TorrentPreferencesR199.RememberLoadedTorrents || Interlocked.Exchange(ref _torrentRestoreGateR199, 1) != 0) return;
        try
        {
            var document = await ReadTorrentSessionWithRecoveryR1646Async();
            if (document?.Torrents is not { Count: > 0 }) return;
            await RestoreEntriesR199Async(
                document.Torrents.OrderBy(x => x.QueuePosition).ToList(),
                "Loaded torrents restored from the previous MediaDock session.");
        }
        catch (Exception ex)
        {
            global::MediaDownloader.App.WriteCrashLog("Torrent.Session.Restore.R1646", ex);
            TorrentStatusR1644 = "Saved torrents could not be restored yet. Their session entries were preserved for the next launch.";
        }
        finally { Volatile.Write(ref _torrentRestoreGateR199, 0); }
    }

    private static string? ResolveTorrentRestoreSourceR1646(TorrentSessionEntryR199 entry)
    {
        // MEDIADOCK_TORRENT_SELF_CONTAINED_SESSION_R1653
        // Magnets are already self-contained. Local .torrent files are copied into a
        // content-addressed app-owned store. If every external/cache file disappeared,
        // rebuild the canonical .torrent from session.json itself.
        foreach (var candidate in new[] { entry.PersistentSource, entry.Source })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var value = candidate.Trim();
            if (value.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) &&
                TorrentClientR1644.IsTorrentSourceR1644(value))
            {
                return value;
            }
        }

        foreach (var candidate in new[] { entry.PersistentSource, entry.Source })
        {
            var canonical = TryCanonicalizeTorrentMetadataFileR1653(candidate);
            if (!string.IsNullOrWhiteSpace(canonical))
            {
                entry.PersistentSource = canonical;
                if (string.IsNullOrWhiteSpace(entry.MetadataBase64))
                {
                    entry.MetadataBase64 = TryReadTorrentMetadataBase64R1653(canonical, string.Empty);
                }
                return canonical;
            }
        }

        if (!string.IsNullOrWhiteSpace(entry.MetadataBase64))
        {
            try
            {
                var metadata = Convert.FromBase64String(entry.MetadataBase64);
                var canonical = CanonicalizeTorrentMetadataBytesR1653(metadata);
                entry.PersistentSource = canonical;
                return canonical;
            }
            catch (Exception ex)
            {
                global::MediaDownloader.App.WriteCrashLog("Torrent.Session.MetadataRehydrate.R1653", ex);
            }
        }

        // Remote .torrent URLs are a final fallback for legacy sessions which predate
        // embedded metadata. A successful prepare will migrate them into the canonical store.
        foreach (var candidate in new[] { entry.PersistentSource, entry.Source })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var value = candidate.Trim();
            if (TorrentClientR1644.IsTorrentSourceR1644(value)) return value;
        }
        return null;
    }

    private async Task RestartTorrentClientForRestoreR1646Async()
    {
        var failed = _torrentClientR1644;
        _torrentClientR1644 = null;
        if (failed is null) return;
        try { await failed.DisposeAsync(); }
        catch (Exception ex) { global::MediaDownloader.App.WriteCrashLog("Torrent.Session.RestoreHostReset.R1646", ex); }
    }

    private async Task RestoreEntriesR199Async(IReadOnlyList<TorrentSessionEntryR199> entries, string completionMessage)
    {
        var restored = 0;
        var retained = new List<TorrentSessionEntryR199>();

        foreach (var entry in entries.OrderBy(x => x.QueuePosition))
        {
            var source = ResolveTorrentRestoreSourceR1646(entry);
            if (source is null)
            {
                retained.Add(entry);
                global::MediaDownloader.App.WriteCrashLog(
                    "Torrent.Session.RestoreSourceMissing.R1646",
                    new FileNotFoundException("Saved torrent metadata/source is temporarily unavailable.", entry.PersistentSource));
                continue;
            }

            try
            {
                TorrentPreviewR1644? preview = null;
                Exception? prepareFailure = null;
                for (var attempt = 0; attempt < 2 && preview is null; attempt++)
                {
                    try
                    {
                        preview = await GetTorrentClientR1644().PrepareAsync(source, CancellationToken.None);
                    }
                    catch (Exception ex) when (TorrentClientR1644.IsHostCommunicationFailureR196(ex) && attempt == 0)
                    {
                        prepareFailure = ex;
                        await RestartTorrentClientForRestoreR1646Async();
                        await Task.Delay(350);
                    }
                }

                if (preview is null)
                {
                    throw prepareFailure ?? new InvalidOperationException("Torrent metadata could not be prepared for restore.");
                }

                var desired = string.IsNullOrWhiteSpace(entry.DesiredState) ? "Running" : entry.DesiredState;
                var shouldRun = TorrentPreferencesR199.ResumeLoadedTorrents &&
                                string.Equals(desired, "Running", StringComparison.OrdinalIgnoreCase) &&
                                CountActiveDownloadsR199() < TorrentPreferencesR199.MaximumActiveDownloads;
                var restoredDesired = string.Equals(desired, "Running", StringComparison.OrdinalIgnoreCase) && !shouldRun ? "Queued" : desired;
                await AddPreparedTorrentInternalR199Async(preview, entry.SavePath, true, shouldRun, entry.CreateSubfolder, persist: false, restoredDesired, entry.Files);
                restored++;
            }
            catch (Exception ex)
            {
                retained.Add(entry);
                global::MediaDownloader.App.WriteCrashLog("Torrent.Session.RestoreItem.R1646", ex);
            }
        }

        UpdateQueuePositionsR199();
        _retainedTorrentSessionEntriesR1646.Clear();
        _retainedTorrentSessionEntriesR1646.AddRange(retained);

        if (retained.Count == 0)
        {
            await SaveTorrentSessionR199Async();
        }
        else
        {
            await SaveTorrentSessionWithRetainedEntriesR1646Async(retained);
        }

        TorrentStatusR1644 = restored > 0
            ? retained.Count == 0
                ? completionMessage
                : $"{completionMessage} {retained.Count} unavailable torrent(s) were kept for automatic retry next launch."
            : retained.Count > 0
                ? "Saved torrents are still unavailable, but MediaDock kept every session entry for automatic retry."
                : "No saved torrents were restored.";
    }

    private async Task SaveTorrentSessionWithRetainedEntriesR1646Async(IReadOnlyList<TorrentSessionEntryR199> retained)
    {
        if (!TorrentPreferencesR199.RememberLoadedTorrents) return;
        await _torrentStateGateR199.WaitAsync();
        try
        {
            _retainedTorrentSessionEntriesR1646.Clear();
            _retainedTorrentSessionEntriesR1646.AddRange(retained);
            WriteTorrentSessionAtomicR1646(BuildTorrentSessionDocumentR1646());
        }
        catch (Exception ex)
        {
            global::MediaDownloader.App.WriteCrashLog("Torrent.Session.SaveRetained.R1646", ex);
        }
        finally { _torrentStateGateR199.Release(); }
    }

    private static string SessionIdentityR1646(TorrentSessionEntryR199 entry)
        => (!string.IsNullOrWhiteSpace(entry.PersistentSource) ? entry.PersistentSource : entry.Source).Trim();

    private static void WriteTorrentSessionAtomicR1646(TorrentSessionDocumentR199 document)
    {
        Directory.CreateDirectory(TorrentStateRootR199);
        var temp = TorrentSessionPathR199 + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(document, TorrentJsonR199));
            if (File.Exists(TorrentSessionPathR199))
            {
                try
                {
                    var current = JsonSerializer.Deserialize<TorrentSessionDocumentR199>(
                        File.ReadAllText(TorrentSessionPathR199),
                        TorrentJsonR199);
                    if (current is not null)
                    {
                        File.Copy(TorrentSessionPathR199, TorrentSessionBackupPathR1646, true);
                    }
                }
                catch
                {
                    // Preserve the previous known-good backup when the primary is corrupt.
                }
            }
            File.Move(temp, TorrentSessionPathR199, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static void WriteJsonAtomicR199<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, TorrentJsonR199));
        File.Move(temp, path, true);
    }

    public async Task DisposeTorrentR1644Async()
    {
        if (Interlocked.Exchange(ref _torrentDisposeGateR1644, 1) != 0) return;
        _torrentRefreshTimerR1644.Stop();
        await SaveTorrentSessionR199Async();
        var client = _torrentClientR1644;
        _torrentClientR1644 = null;
        if (client is not null)
        {
            try { await client.DisposeAsync(); }
            catch (Exception ex) { global::MediaDownloader.App.WriteCrashLog("Torrent.IsolatedHost.Dispose.R199", ex); }
        }
        _torrentStateGateR199.Dispose();
    }
}
