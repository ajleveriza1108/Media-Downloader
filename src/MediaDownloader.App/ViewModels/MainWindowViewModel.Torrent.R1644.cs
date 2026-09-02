using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        public int Version { get; set; } = 1;
        public List<TorrentSessionEntryR199> Torrents { get; set; } = [];
    }

    private sealed class TorrentSessionEntryR199
    {
        public string Source { get; set; } = string.Empty;
        public string PersistentSource { get; set; } = string.Empty;
        public string SavePath { get; set; } = string.Empty;
        public string DesiredState { get; set; } = "Running";
        public int QueuePosition { get; set; }
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
    private readonly DispatcherTimer _torrentRefreshTimerR1644 = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private readonly SemaphoreSlim _torrentStateGateR199 = new(1, 1);
    private string _torrentSourceR1644 = string.Empty;
    private string _torrentStatusR1644 = "Open a .torrent or paste a magnet link. Downloads start automatically in Fast mode.";
    private TorrentItemR1644? _selectedTorrentR1644;
    private int _torrentDisposeGateR1644;
    private int _torrentRefreshGateR1644;
    private int _torrentQueueGateR199;
    private int _torrentRestoreGateR199;

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
            Directory.CreateDirectory(TorrentOutputDirectoryR1644);
        }
        catch (Exception ex)
        {
            TorrentStatusR1644 = "Torrent folder is not writable.";
            global::MediaDownloader.App.WriteCrashLog("Torrent.OutputDirectory.R199", ex);
        }

        _torrentRefreshTimerR1644.Tick += async (_, _) => await RefreshTorrentTelemetryR190Async();
        _torrentRefreshTimerR1644.Start();
        _ = RestorePersistedTorrentsR199Async();
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
        var currentEntries = BuildSessionEntriesR199();
        TorrentPreferencesR199 = settings.Clone();
        Directory.CreateDirectory(TorrentOutputDirectoryR1644);
        WriteJsonAtomicR199(TorrentSettingsPathR199, TorrentPreferencesR199);
        if (TorrentPreferencesR199.RememberLoadedTorrents)
        {
            await SaveTorrentSessionR199Async();
        }
        else
        {
            try { if (File.Exists(TorrentSessionPathR199)) File.Delete(TorrentSessionPathR199); }
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
        bool startImmediately)
        => await AddPreparedTorrentInternalR199Async(preview, savePath, streaming, startImmediately, persist: true, desiredState: null, persistedFiles: null);

    private async Task<TorrentItemR1644> AddPreparedTorrentInternalR199Async(
        TorrentPreviewR1644 preview,
        string savePath,
        bool streaming,
        bool startImmediately,
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

        var allowStart = startImmediately && TorrentPreferencesR199.AutoStartDownloads && CountActiveDownloadsR199() < TorrentPreferencesR199.MaximumActiveDownloads;
        TorrentStatusR1644 = allowStart ? "Adding torrent and starting Fast download..." : "Adding torrent to the queue...";

        var result = await GetTorrentClientR1644().AddPreparedAsync(preview, targetFolder, streaming, allowStart, CancellationToken.None);
        var snapshot = result.Snapshot;
        var item = new TorrentItemR1644
        {
            Id = result.TorrentId,
            Source = preview.Source,
            PersistentSource = string.IsNullOrWhiteSpace(preview.PersistentSource) ? preview.Source : preview.PersistentSource,
            SavePath = targetFolder,
            AddedUtc = snapshot.AddedUtc
        };
        item.SetDesiredStateR199(desiredState ?? (allowStart ? "Running" : "Queued"));
        item.ApplySnapshot(snapshot);

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
            foreach (var item in TorrentsR1644.Where(x => string.Equals(x.DesiredStateR199, "Queued", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                if (active >= TorrentPreferencesR199.MaximumActiveDownloads) break;
                try
                {
                    item.SetDesiredStateR199("Running");
                    item.ApplySnapshot(await GetTorrentClientR1644().StartAsync(item.Id));
                    active++;
                }
                catch (Exception ex)
                {
                    item.MarkOperationError(ex.Message);
                }
            }
            await SaveTorrentSessionR199Async();
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

    private List<TorrentSessionEntryR199> BuildSessionEntriesR199()
    {
        UpdateQueuePositionsR199();
        return TorrentsR1644.Select(item => new TorrentSessionEntryR199
        {
            Source = item.Source,
            PersistentSource = item.PersistentSource,
            SavePath = item.SavePath,
            DesiredState = item.DesiredStateR199,
            QueuePosition = item.QueuePosition,
            Files = item.PersistedFileChoicesR199.Select(file => new TorrentSessionFileR199
            {
                Index = file.Index,
                Selected = file.Selected,
                Priority = file.Priority.ToString()
            }).ToList()
        }).ToList();
    }

    private async Task SaveTorrentSessionR199Async()
    {
        if (!TorrentPreferencesR199.RememberLoadedTorrents) return;
        await _torrentStateGateR199.WaitAsync();
        try
        {
            Directory.CreateDirectory(TorrentStateRootR199);
            WriteJsonAtomicR199(TorrentSessionPathR199, new TorrentSessionDocumentR199 { Torrents = BuildSessionEntriesR199() });
        }
        catch (Exception ex) { global::MediaDownloader.App.WriteCrashLog("Torrent.Session.Save.R199", ex); }
        finally { _torrentStateGateR199.Release(); }
    }

    private async Task RestorePersistedTorrentsR199Async()
    {
        if (!TorrentPreferencesR199.RememberLoadedTorrents || Interlocked.Exchange(ref _torrentRestoreGateR199, 1) != 0) return;
        try
        {
            if (!File.Exists(TorrentSessionPathR199)) return;
            var document = JsonSerializer.Deserialize<TorrentSessionDocumentR199>(await File.ReadAllTextAsync(TorrentSessionPathR199), TorrentJsonR199);
            if (document?.Torrents is not { Count: > 0 }) return;
            await RestoreEntriesR199Async(document.Torrents.OrderBy(x => x.QueuePosition).ToList(), "Loaded torrents restored from the previous MediaDock session.");
        }
        catch (Exception ex)
        {
            global::MediaDownloader.App.WriteCrashLog("Torrent.Session.Restore.R199", ex);
            TorrentStatusR1644 = "Some saved torrents could not be restored. The session file was kept for diagnostics.";
        }
        finally { Volatile.Write(ref _torrentRestoreGateR199, 0); }
    }

    private async Task RestoreEntriesR199Async(IReadOnlyList<TorrentSessionEntryR199> entries, string completionMessage)
    {
        var restored = 0;
        foreach (var entry in entries.OrderBy(x => x.QueuePosition))
        {
            var source = !string.IsNullOrWhiteSpace(entry.PersistentSource) ? entry.PersistentSource : entry.Source;
            if (!TorrentClientR1644.IsTorrentSourceR1644(source)) continue;
            try
            {
                var preview = await GetTorrentClientR1644().PrepareAsync(source, CancellationToken.None);
                var desired = string.IsNullOrWhiteSpace(entry.DesiredState) ? "Running" : entry.DesiredState;
                var shouldRun = TorrentPreferencesR199.ResumeLoadedTorrents &&
                                string.Equals(desired, "Running", StringComparison.OrdinalIgnoreCase) &&
                                CountActiveDownloadsR199() < TorrentPreferencesR199.MaximumActiveDownloads;
                var restoredDesired = string.Equals(desired, "Running", StringComparison.OrdinalIgnoreCase) && !shouldRun ? "Queued" : desired;
                await AddPreparedTorrentInternalR199Async(preview, entry.SavePath, true, shouldRun, persist: false, restoredDesired, entry.Files);
                restored++;
            }
            catch (Exception ex) { global::MediaDownloader.App.WriteCrashLog("Torrent.Session.RestoreItem.R199", ex); }
        }
        UpdateQueuePositionsR199();
        await SaveTorrentSessionR199Async();
        TorrentStatusR1644 = restored > 0 ? completionMessage : "No saved torrents were restored.";
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
