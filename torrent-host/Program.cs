using System.Net;
using System.Text;
using System.Text.Json;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Dht;

namespace MediaDock.TorrentHost;

internal static class Program
{
    private const string ProtocolPrefix = "MDTH1 ";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<int> Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception ex)
            {
                TorrentHostRuntime.WriteCrashLog("AppDomain.UnhandledException", ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            TorrentHostRuntime.WriteCrashLog("TaskScheduler.UnobservedTaskException", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        if (args.Any(arg => string.Equals(arg, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                Environment.SetEnvironmentVariable("MEDIADOCK_TORRENT_SELFTEST", "1");
                await TorrentHostRuntime.RunSelfTestAsync();
                Console.WriteLine("MEDIADOCK_TORRENT_HOST_SELF_TEST=PASS");
                return 0;
            }
            catch (Exception ex)
            {
                TorrentHostRuntime.WriteCrashLog("SelfTest", ex);
                Console.Error.WriteLine(ex);
                return 17;
            }
        }

        // Reserve the original stdout stream exclusively for framed IPC. Any library or
        // diagnostic Console.Write/WriteLine call is redirected to stderr so it can never
        // corrupt the JSON protocol consumed by MediaDock.
        //
        // R1.9.10 lifetime contract: redirected stdin owns the host lifetime. When MediaDock
        // exits, the pipe closes, ReadLineAsync returns null, and TorrentHost exits naturally.
        // No parent-PID watcher is used.
        var protocolOut = Console.Out;
        Console.SetOut(Console.Error);

        try
        {
            await using var runtime = new TorrentHostRuntime();
            string? line;
            while ((line = await Console.In.ReadLineAsync()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                TorrentHostResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<TorrentHostRequest>(line, JsonOptions)
                        ?? throw new InvalidDataException("Torrent host request was empty.");
                    response = await runtime.HandleAsync(request);
                }
                catch (Exception ex)
                {
                    TorrentHostRuntime.WriteCrashLog("Request", ex);
                    response = new TorrentHostResponse
                    {
                        RequestId = string.Empty,
                        Ok = false,
                        Error = TorrentHostRuntime.FriendlyError(ex)
                    };
                }

                try
                {
                    var responseJson = JsonSerializer.Serialize(response, JsonOptions);
                    await protocolOut.WriteLineAsync(ProtocolPrefix + responseJson);
                    await protocolOut.FlushAsync();
                }
                catch (Exception ex)
                {
                    TorrentHostRuntime.WriteCrashLog("Protocol.WriteResponse", ex);
                    Console.Error.WriteLine(ex);
                    return 23;
                }

                if (runtime.ShutdownRequested)
                {
                    break;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            TorrentHostRuntime.WriteCrashLog("Fatal.IpcLoop", ex);
            Console.Error.WriteLine(ex);
            return 19;
        }
    }

}

internal sealed class TorrentHostRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
}

internal sealed class TorrentHostResponse
{
    public string RequestId { get; set; } = string.Empty;
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public object? Data { get; set; }
}

internal sealed class PreparedTorrent
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string PersistentSource { get; init; }
    public Torrent? Metadata { get; init; }
    public MagnetLink? Magnet { get; init; }
}

internal sealed class ManagedTorrent
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string PersistentSource { get; init; }
    public required string SavePath { get; init; }
    public required TorrentManager Manager { get; init; }
    public DateTimeOffset AddedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string LastError { get; set; } = string.Empty;
    public string DiscoveryStatus { get; set; } = "Starting peer discovery...";
    public string LastTrackerStatus { get; set; } = "Waiting for tracker announce";
    public string LastPeerFailure { get; set; } = string.Empty;
    public long DiscoveredPeers;
    // MEDIADOCK_TORRENT_LIVE_TELEMETRY_R1646
    public long LastSnapshotDownloaded;
    public double LastSnapshotVerifiedProgress;
    public DateTimeOffset LastTransferUtc = DateTimeOffset.MinValue;
    public int LiveProgressBaselineGate;
    public long LiveProgressBaselineDownloaded;
    public double LiveProgressBaselineVerified;
    // MEDIADOCK_TORRENT_MEASURED_RATE_R1647
    // MonoTorrent's aggregate Monitor rate can briefly report zero while payload bytes
    // are still advancing. Measure the transfer from DataBytesReceived/DataBytesSent
    // over a monotonic time window and retain peer-monitor telemetry as a fallback.
    public long RateSampleDownloaded;
    public long RateSampleUploaded;
    public long RateSampleTick;
    public long LastDownloadActivityTick;
    public long LastUploadActivityTick;
    public long MeasuredDownloadRate;
    public long MeasuredUploadRate;
    // MEDIADOCK_TORRENT_SOURCE_TELEMETRY_R1646
    public long TrackerPeersDiscovered;
    public long DhtPeersDiscovered;
    public long PexPeersDiscovered;
    public long LocalPeersDiscovered;
    public long OtherPeersDiscovered;
    public long ConnectionFailures;
    // MEDIADOCK_TORRENT_HOT_STATUS_CACHE_R1646
    // Keep the high-frequency status IPC path free of peer enumeration and tracker I/O.
    public int CachedConnectedPeers;
    public int CachedConnectedSeeds;
    public long CachedPeerDownloadRate;
    public long CachedPeerUploadRate;
    public long LastPeerDetailRefreshTick;
    public int PeerDetailRefreshGate;
    public long LastTrackerScrapeAttemptTick;
    public int TrackerScrapeGate;
    public int StartedAnnounceGate;
    public int RecoveryLoopGate;
    public int Removed;
}

internal sealed class TorrentHostSettingsR199
{
    // The peer listener is always enabled in normal runtime so tracker announces contain
    // a valid port. Router mapping/inbound reachability remains an explicit preference.
    public bool EnablePeerListener { get; set; } = true;
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
    public int PeerRecoveryAttempts { get; set; } = 30;
    public int PeerRecoveryIntervalSeconds { get; set; } = 4;

    public static TorrentHostSettingsR199 Load()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AJCoder", "MediaDock", "TorrentClient", "settings.json");
            if (!File.Exists(path)) return new();
            return JsonSerializer.Deserialize<TorrentHostSettingsR199>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch { return new(); }
    }
}

internal sealed class TorrentHostRuntime : IAsyncDisposable
{
    private const int MaxTorrentMetadataBytes = 64 * 1024 * 1024;
    private static readonly TimeSpan StartSettleTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan RemoteTimeout = TimeSpan.FromSeconds(30);

    private readonly ClientEngine _engine;
    private readonly TorrentHostSettingsR199 _settingsR199;
    private readonly HttpClient _http;
    private readonly string _metadataDirectory;
    private readonly Dictionary<string, PreparedTorrent> _prepared = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ManagedTorrent> _managed = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private int _disposeGate;

    public bool ShutdownRequested { get; private set; }

    public TorrentHostRuntime()
    {
        _settingsR199 = TorrentHostSettingsR199.Load();
        var networkSelfTest = string.Equals(Environment.GetEnvironmentVariable("MEDIADOCK_TORRENT_SELFTEST"), "1", StringComparison.Ordinal);

        // MEDIADOCK_TORRENT_CANONICAL_METADATA_STORE_R1653
        // Keep .torrent metadata beside the persistent session, not under the disposable
        // TorrentHost runtime/cache tree. Self-tests are redirected to their temp workspace.
        var selfTestMetadataDirectory = Environment.GetEnvironmentVariable("MEDIADOCK_TORRENT_SELFTEST_METADATA_DIR");
        _metadataDirectory = networkSelfTest && !string.IsNullOrWhiteSpace(selfTestMetadataDirectory)
            ? selfTestMetadataDirectory
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AJCoder",
                "MediaDock",
                "TorrentClient",
                "Metadata");
        Directory.CreateDirectory(_metadataDirectory);
        if (!networkSelfTest) MigrateLegacyTorrentMetadataR1653();

        _http = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = RemoteTimeout
        };

        // R1.9.10 Fast mode keeps unrestricted transfer rates but uses a stable cache path so
        // DHT can bootstrap from prior sessions instead of starting from zero after every test build.
        // Peer discovery recovery below re-announces trackers/DHT/LPD when a started torrent remains idle.
        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AJCoder",
            "MediaDock",
            "TorrentHost",
            "Cache");
        Directory.CreateDirectory(cacheDirectory);

        // MEDIADOCK_TORRENT_FAST_PEER_BOOTSTRAP_R1910
        var settings = new EngineSettingsBuilder
        {
            AllowHaveSuppression = true,
            AllowLocalPeerDiscovery = !networkSelfTest && _settingsR199.EnableLocalPeerDiscovery,
            AllowPortForwarding = !networkSelfTest && _settingsR199.EnablePortMapping && _settingsR199.EnableIncomingConnections,
            AutoSaveLoadDhtCache = !networkSelfTest,
            AutoSaveLoadFastResume = !networkSelfTest && _settingsR199.EnableFastResume,
            AutoSaveLoadMagnetLinkMetadata = !networkSelfTest,
            CacheDirectory = cacheDirectory,
            ConnectionTimeouts = new List<TimeSpan>
            {
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(8),
                TimeSpan.FromSeconds(10)
            },
            ConnectionRetryDelays = new List<TimeSpan>
            {
                TimeSpan.FromSeconds(4),
                TimeSpan.FromSeconds(12),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(60)
            },
            DhtBootstrapRouters = new List<BootstrapRouter>
            {
                new("router.bittorrent.com", 6881),
                new("router.utorrent.com", 6881),
                new("dht.transmissionbt.com", 6881),
                new("dht.aelitis.com", 6881),
                new("router.bitcomet.com", 6881),
                new("dht.libtorrent.org", 25401)
            },
            DiskCacheBytes = 96 * 1024 * 1024,
            MaximumConnections = Math.Clamp(_settingsR199.MaximumConnections, 20, 2000),
            MaximumHalfOpenConnections = Math.Clamp(_settingsR199.MaximumHalfOpenConnections, 8, 256),
            MaximumOpenFiles = 128,
            MaximumDownloadRate = Math.Max(0, _settingsR199.MaximumDownloadRateKbps) * 1024,
            MaximumUploadRate = Math.Max(0, _settingsR199.MaximumUploadRateKbps) * 1024,
            UsePartialFiles = true,
            // MEDIADOCK_TORRENT_VALID_TRACKER_PORT_R1646
            // A BitTorrent tracker announce needs a real peer port. R1.6.46 disabled the
            // listener whenever router-mapped incoming connections were disabled, which
            // left MonoTorrent with no actual port to report. Bind an automatic local peer
            // port in every normal run; UPnP/NAT-PMP remains opt-in through AllowPortForwarding.
            ListenEndPoints = !networkSelfTest && _settingsR199.EnablePeerListener
                ? new Dictionary<string, IPEndPoint>
                {
                    ["ipv4"] = new(IPAddress.Any, _settingsR199.ListeningPort),
                    ["ipv6"] = new(IPAddress.IPv6Any, _settingsR199.ListeningPort)
                }
                : new Dictionary<string, IPEndPoint>(),
            DhtEndPoint = !networkSelfTest && _settingsR199.EnableDht ? new IPEndPoint(IPAddress.Any, _settingsR199.DhtPort) : null,
            HttpStreamingPrefix = "http://127.0.0.1:55126/"
        };
        _engine = new ClientEngine(settings.ToSettings());
    }

    public async Task<TorrentHostResponse> HandleAsync(TorrentHostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            request.RequestId = Guid.NewGuid().ToString("N");
        }

        await _commandGate.WaitAsync();
        try
        {
            ThrowIfDisposed();
            object? data = request.Command.Trim().ToLowerInvariant() switch
            {
                "ping" => new
                {
                    Version = "R1.6.56",
                    ProcessId = Environment.ProcessId,
                    Engine = "MonoTorrent 3.9 alpha",
                    DhtState = _engine.Dht.State.ToString(),
                    DhtNodes = _engine.Dht.NodeCount,
                    PeerListenerConfigured = _engine.Settings.ListenEndPoints.Count > 0
                },
                "prepare" => await PrepareAsync(request.Payload),
                "discardprepared" => DiscardPrepared(request.Payload),
                "add" => await AddAsync(request.Payload),
                "status" => await GetStatusAsync(),
                "start" => await StartAsync(request.Payload),
                "pause" => await PauseAsync(request.Payload),
                "stop" => await StopAsync(request.Payload),
                "recheck" => await RecheckAsync(request.Payload),
                "announce" => await AnnounceNowR199Async(request.Payload),
                "remove" => await RemoveAsync(request.Payload),
                "files" => GetFiles(request.Payload),
                "setfiles" => await SetFilesAsync(request.Payload),
                "peers" => await GetPeersAsync(request.Payload),
                "trackers" => GetTrackers(request.Payload),
                "stream" => await CreateStreamAsync(request.Payload),
                "shutdown" => RequestShutdown(),
                _ => throw new InvalidOperationException($"Unknown torrent host command '{request.Command}'.")
            };

            return new TorrentHostResponse
            {
                RequestId = request.RequestId,
                Ok = true,
                Data = data
            };
        }
        catch (Exception ex)
        {
            WriteCrashLog($"Command.{request.Command}", ex);
            return new TorrentHostResponse
            {
                RequestId = request.RequestId,
                Ok = false,
                Error = FriendlyError(ex)
            };
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private void MigrateLegacyTorrentMetadataR1653()
    {
        try
        {
            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AJCoder", "MediaDock", "TorrentHost", "Metadata");
            if (!Directory.Exists(legacy) ||
                string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(_metadataDirectory), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var source in Directory.EnumerateFiles(legacy, "*.torrent", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var destination = Path.Combine(_metadataDirectory, Path.GetFileName(source));
                    if (!File.Exists(destination)) File.Copy(source, destination, false);
                }
                catch (Exception ex)
                {
                    WriteCrashLog("MetadataMigration.R1653", ex);
                }
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("MetadataMigration.Scan.R1653", ex);
        }
    }

    private object RequestShutdown()
    {
        ShutdownRequested = true;
        return new { ShuttingDown = true };
    }

    private async Task<object> PrepareAsync(JsonElement payload)
    {
        var source = RequiredString(payload, "Source").Trim();
        if (source.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            if (!MagnetLink.TryParse(source, out var magnet))
            {
                throw new InvalidDataException("The magnet link is invalid.");
            }

            var id = Guid.NewGuid().ToString("N");
            _prepared[id] = new PreparedTorrent { Id = id, Source = source, PersistentSource = source, Magnet = magnet };
            return new
            {
                PreviewId = id,
                Source = source,
                PersistentSource = source,
                Name = string.IsNullOrWhiteSpace(magnet.Name) ? "Magnet torrent" : magnet.Name,
                TotalSize = magnet.Size ?? 0,
                IsMagnet = true,
                Files = Array.Empty<object>()
            };
        }

        var path = await ResolveTorrentFileAsync(source, CancellationToken.None);
        await ValidateTorrentEnvelopeAsync(path, CancellationToken.None);
        var torrent = await Torrent.LoadAsync(path);
        var hashName = torrent.InfoHashes.V1?.ToHex() ?? torrent.InfoHashes.V2?.ToHex() ?? Guid.NewGuid().ToString("N");
        var persistentPath = Path.Combine(_metadataDirectory, hashName + ".torrent");
        if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(persistentPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(path, persistentPath, true);
        }
        if (torrent.Files.Count == 0)
        {
            throw new InvalidDataException("The .torrent file contains no downloadable files.");
        }

        var previewId = Guid.NewGuid().ToString("N");
        _prepared[previewId] = new PreparedTorrent
        {
            Id = previewId,
            Source = source,
            PersistentSource = persistentPath,
            Metadata = torrent
        };

        return new
        {
            PreviewId = previewId,
            Source = source,
            PersistentSource = persistentPath,
            Name = torrent.Name,
            TotalSize = torrent.Size,
            IsMagnet = false,
            Files = torrent.Files.Select((file, index) => new
            {
                Index = index,
                Path = file.Path,
                Length = file.Length,
                Selected = true,
                Priority = "Normal"
            }).ToArray()
        };
    }

    private object DiscardPrepared(JsonElement payload)
    {
        var previewId = RequiredString(payload, "PreviewId");
        _prepared.Remove(previewId);
        return new { Discarded = true };
    }

    private async Task<object> AddAsync(JsonElement payload)
    {
        var previewId = RequiredString(payload, "PreviewId");
        var savePath = RequiredString(payload, "SavePath").Trim();
        var startImmediately = OptionalBool(payload, "StartImmediately", true);
        var enableStreaming = OptionalBool(payload, "EnableStreaming", true);
        var createSubfolder = OptionalBool(payload, "CreateSubfolder", true);

        if (!_prepared.Remove(previewId, out var prepared))
        {
            throw new InvalidOperationException("The prepared torrent expired. Open the .torrent file again.");
        }

        Directory.CreateDirectory(savePath);
        var torrentSettings = new TorrentSettingsBuilder
        {
            MaximumConnections = Math.Clamp(_settingsR199.MaximumPeersPerTorrent, 10, 1000),
            UploadSlots = Math.Clamp(_settingsR199.UploadSlotsPerTorrent, 1, 100),
            MaximumDownloadRate = Math.Max(0, _settingsR199.MaximumDownloadRateKbps) * 1024,
            MaximumUploadRate = Math.Max(0, _settingsR199.MaximumUploadRateKbps) * 1024,
            AllowDht = _settingsR199.EnableDht,
            AllowPeerExchange = _settingsR199.EnablePex,
            CreateContainingDirectory = createSubfolder
        }.ToSettings();

        TorrentManager manager;
        if (prepared.Metadata is not null)
        {
            if (_engine.Contains(prepared.Metadata))
            {
                throw new InvalidOperationException("This torrent is already registered in the torrent engine.");
            }

            manager = enableStreaming
                ? await _engine.AddStreamingAsync(prepared.Metadata, savePath, torrentSettings)
                : await _engine.AddAsync(prepared.Metadata, savePath, torrentSettings);

            if (TryGetProperty(payload, "Files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
            {
                await ApplyFileSelectionsAsync(manager, filesElement);
            }
        }
        else if (prepared.Magnet is not null)
        {
            if (_engine.Contains(prepared.Magnet.InfoHashes))
            {
                throw new InvalidOperationException("This magnet torrent is already registered in the torrent engine.");
            }

            manager = enableStreaming
                ? await _engine.AddStreamingAsync(prepared.Magnet, savePath, torrentSettings)
                : await _engine.AddAsync(prepared.Magnet, savePath, torrentSettings);
        }
        else
        {
            throw new InvalidOperationException("Prepared torrent metadata was unavailable.");
        }

        // MEDIADOCK_TORRENT_TRACKERLESS_MAGNET_BOOTSTRAP_R1646
        if (_settingsR199.FastPeerDiscovery && _settingsR199.UsePublicTrackerFallback)
        {
            if (manager.HasMetadata && manager.Torrent is { IsPrivate: false })
            {
                await AddPublicTrackerFallbacksR199(manager);
            }
            else if (!manager.HasMetadata && manager.TrackerManager.Tiers.Count == 0)
            {
                // A trackerless magnet otherwise has to wait for a cold DHT bootstrap.
                // Give it immediate HTTPS/UDP discovery paths while metadata is pending.
                await AddPublicTrackerFallbacksR199(manager);
            }
        }

        var managed = new ManagedTorrent
        {
            Id = Guid.NewGuid().ToString("N"),
            Source = prepared.Source,
            PersistentSource = prepared.PersistentSource,
            SavePath = savePath,
            Manager = manager
        };
        WirePeerDiscoveryTelemetryR198(managed);
        _managed[managed.Id] = managed;

        string? startError = null;
        if (startImmediately)
        {
            try
            {
                await StartAndSettleAsync(managed);
                StartPeerDiscoveryRecoveryR198(managed);
            }
            catch (Exception ex)
            {
                managed.LastError = FriendlyError(ex);
                startError = managed.LastError;
                await TryStopManagerAsync(manager);
            }
        }

        return new
        {
            TorrentId = managed.Id,
            StartError = startError,
            Snapshot = await BuildSnapshotAsync(managed)
        };
    }

    private async Task<object> GetStatusAsync()
    {
        var result = new List<object>(_managed.Count);
        foreach (var item in _managed.Values.ToArray())
        {
            try
            {
                result.Add(await BuildSnapshotAsync(item));
            }
            catch (Exception ex)
            {
                item.LastError = FriendlyError(ex);
                result.Add(new
                {
                    Id = item.Id,
                    Source = item.Source,
                    PersistentSource = item.PersistentSource,
                    SavePath = item.SavePath,
                    Name = SafeManagerName(item.Manager),
                    Status = "Torrent engine status unavailable",
                    Progress = 0d,
                    TotalSize = 0L,
                    DownloadRate = 0L,
                    UploadRate = 0L,
                    Peers = 0,
                    Seeds = 0,
                    EtaSeconds = -1d,
                    Downloaded = 0L,
                    Uploaded = 0L,
                    Ratio = 0d,
                    LastError = item.LastError,
                    StreamingAvailable = item.Manager.StreamProvider is not null,
                    HasMetadata = item.Manager.HasMetadata,
                    AddedUtc = item.AddedUtc
                });
            }
        }
        return result;
    }

    private async Task<object> StartAsync(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        item.LastError = string.Empty;
        item.DiscoveryStatus = "Starting peer discovery...";
        await StartAndSettleAsync(item);
        StartPeerDiscoveryRecoveryR198(item);
        return await BuildSnapshotAsync(item);
    }

    private async Task<object> PauseAsync(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        await item.Manager.PauseAsync().WaitAsync(TimeSpan.FromSeconds(10));
        item.LastError = string.Empty;
        return await BuildSnapshotAsync(item);
    }

    private async Task<object> StopAsync(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        await SafeStopAsync(item.Manager);
        item.LastError = string.Empty;
        return await BuildSnapshotAsync(item);
    }

    private async Task<object> RecheckAsync(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        if (!item.Manager.HasMetadata)
        {
            throw new InvalidOperationException("Torrent metadata must be available before rechecking files.");
        }

        await SafeStopAsync(item.Manager);
        await item.Manager.HashCheckAsync(false).WaitAsync(TimeSpan.FromMinutes(15));
        ResetLiveProgressBaselineR1646(item);
        item.LastError = string.Empty;
        return await BuildSnapshotAsync(item);
    }

    private async Task<object> AnnounceNowR199Async(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        item.DiscoveryStatus = "Updating trackers, DHT and peer discovery...";
        if (_settingsR199.UsePublicTrackerFallback)
        {
            if (item.Manager.HasMetadata && item.Manager.Torrent is { IsPrivate: false })
                await AddPublicTrackerFallbacksR199(item.Manager);
            else if (!item.Manager.HasMetadata && item.Manager.TrackerManager.Tiers.Count == 0)
                await AddPublicTrackerFallbacksR199(item.Manager);
        }
        // A manual refresh is a normal announce. The Started event is emitted once per run.
        await KickPeerDiscoveryR1910Async(item, sendStartedEvent: false);
        StartPeerDiscoveryRecoveryR198(item);
        return await BuildSnapshotAsync(item);
    }

    private async Task<object> RemoveAsync(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        var deleteData = OptionalBool(payload, "DeleteData", false);
        Volatile.Write(ref item.Removed, 1);
        await SafeStopAsync(item.Manager);
        if (_engine.Contains(item.Manager))
        {
            if (!string.Equals(item.Manager.State.ToString(), "Stopped", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The torrent could not be stopped safely, so it was not removed.");
            }
            await _engine.RemoveAsync(item.Manager);
        }
        _managed.Remove(item.Id);
        if (deleteData)
        {
            DeleteTorrentDataR199(item);
        }
        return new { Removed = true, DeletedData = deleteData, item.Id };
    }

    private static void DeleteTorrentDataR199(ManagedTorrent item)
    {
        var saveRoot = Path.GetFullPath(item.SavePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var file in item.Manager.Files)
        {
            foreach (var candidate in new[] { file.FullPath, file.DownloadCompleteFullPath, file.DownloadIncompleteFullPath })
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(candidate)) continue;
                    var full = Path.GetFullPath(candidate);
                    if (!full.StartsWith(saveRoot, StringComparison.OrdinalIgnoreCase)) continue;
                    if (File.Exists(full)) File.Delete(full);
                }
                catch { }
            }
        }
        try
        {
            var containing = item.Manager.ContainingDirectory;
            if (!string.IsNullOrWhiteSpace(containing) && Directory.Exists(containing) &&
                !string.Equals(Path.GetFullPath(containing).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(item.SavePath).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) &&
                !Directory.EnumerateFileSystemEntries(containing).Any()) Directory.Delete(containing, true);
        }
        catch { }
    }

    private object GetFiles(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        if (!item.Manager.HasMetadata)
        {
            return Array.Empty<object>();
        }

        return item.Manager.Files.Select((file, index) => new
        {
            Index = index,
            Path = file.Path,
            Length = file.Length,
            Progress = file.BitField.PercentComplete,
            Selected = file.Priority != Priority.DoNotDownload,
            Priority = PriorityName(file.Priority)
        }).ToArray();
    }

    private async Task<object> SetFilesAsync(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        if (!item.Manager.HasMetadata)
        {
            throw new InvalidOperationException("Torrent metadata is not available yet.");
        }

        if (!TryGetProperty(payload, "Files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("File priority selection was missing.");
        }

        await ApplyFileSelectionsAsync(item.Manager, filesElement);
        ResetLiveProgressBaselineR1646(item);
        return GetFiles(payload);
    }

    private async Task<object> GetPeersAsync(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        var peers = await item.Manager.GetPeersAsync();
        return peers.Select(peer => new
        {
            Endpoint = peer.Uri?.ToString() ?? "Unknown",
            Direction = peer.ConnectionDirection.ToString(),
            DownloadRate = peer.Monitor.DownloadRate,
            UploadRate = peer.Monitor.UploadRate,
            Encryption = peer.EncryptionType.ToString(),
            IsSeeder = peer.IsSeeder,
            Client = peer.ClientApp.ToString()
        }).ToArray();
    }

    private object GetTrackers(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        return item.Manager.TrackerManager.Tiers.Select(tier => new
        {
            Tracker = tier.ActiveTracker?.ToString() ?? "(none)",
            Announce = tier.LastAnnounceSucceeded ? "OK" : "Waiting / failed",
            Scrape = tier.LastScrapeSucceeded ? "OK" : "Waiting / failed"
        }).ToArray();
    }

    private async Task<object> CreateStreamAsync(JsonElement payload)
    {
        var item = RequiredManaged(payload);
        if (item.Manager.StreamProvider is null)
        {
            throw new InvalidOperationException("Streaming is not enabled for this torrent. Remove and add it again.");
        }

        if (!item.Manager.HasMetadata)
        {
            await StartAndSettleAsync(item);
            using var metadataTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await item.Manager.WaitForMetadataAsync(metadataTimeout.Token);
        }

        var downloadableFiles = item.Manager.Files
            .Where(file => file.Priority != Priority.DoNotDownload)
            .ToArray();
        var file = downloadableFiles
            .Where(file => IsLikelyMedia(file.Path))
            .OrderByDescending(file => file.Length)
            .FirstOrDefault() ?? downloadableFiles.OrderByDescending(file => file.Length).FirstOrDefault();
        if (file is null)
        {
            throw new InvalidOperationException("No file is available to stream.");
        }

        await StartAndSettleAsync(item);
        using var streamTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var stream = await item.Manager.StreamProvider.CreateHttpStreamAsync(file, streamTimeout.Token);
        return new { Url = $"http://127.0.0.1:55126{stream.RelativeUri}" };
    }

    private async Task ApplyFileSelectionsAsync(TorrentManager manager, JsonElement filesElement)
    {
        foreach (var element in filesElement.EnumerateArray())
        {
            var index = RequiredInt(element, "Index");
            if (index < 0 || index >= manager.Files.Count)
            {
                continue;
            }

            var selected = OptionalBool(element, "Selected", true);
            var priorityName = OptionalString(element, "Priority") ?? "Normal";
            var priority = selected ? ParsePriority(priorityName) : Priority.DoNotDownload;
            await manager.SetFilePriorityAsync(manager.Files[index], priority);
        }
    }

    private static void WirePeerDiscoveryTelemetryR198(ManagedTorrent item)
    {
        item.Manager.PeersFound += (_, e) =>
        {
            if (e.NewPeers <= 0) return;

            Interlocked.Add(ref item.DiscoveredPeers, e.NewPeers);

            // MEDIADOCK_TORRENT_SOURCE_TELEMETRY_R1646
            // MonoTorrent exposes distinct derived PeersAddedEventArgs types. Use the
            // runtime type name here to keep this host resilient across 3.9 alpha API
            // revisions while still showing exactly which discovery path is productive.
            var source = e.GetType().Name;
            if (source.Contains("Tracker", StringComparison.OrdinalIgnoreCase))
                Interlocked.Add(ref item.TrackerPeersDiscovered, e.NewPeers);
            else if (source.Contains("Dht", StringComparison.OrdinalIgnoreCase))
                Interlocked.Add(ref item.DhtPeersDiscovered, e.NewPeers);
            else if (source.Contains("Exchange", StringComparison.OrdinalIgnoreCase) ||
                     source.Contains("Pex", StringComparison.OrdinalIgnoreCase))
                Interlocked.Add(ref item.PexPeersDiscovered, e.NewPeers);
            else if (source.Contains("Local", StringComparison.OrdinalIgnoreCase))
                Interlocked.Add(ref item.LocalPeersDiscovered, e.NewPeers);
            else
                Interlocked.Add(ref item.OtherPeersDiscovered, e.NewPeers);

            item.DiscoveryStatus = $"Discovered {Volatile.Read(ref item.DiscoveredPeers)} peer(s); connecting...";
        };

        item.Manager.PeerConnected += (_, e) =>
        {
            item.DiscoveryStatus = $"Connected to {e.Peer.Uri}";
        };

        item.Manager.ConnectionAttemptFailed += (_, e) =>
        {
            Interlocked.Increment(ref item.ConnectionFailures);
            item.LastPeerFailure = $"{e.Peer.ConnectionUri}: {e.Reason}";
        };

        item.Manager.TrackerManager.AnnounceComplete += (_, e) =>
        {
            item.LastTrackerStatus = $"{e.Tracker}: {(e.Successful ? "OK" : "failed")}";
            if (e.Successful)
            {
                item.DiscoveryStatus = "Tracker announce completed; connecting to peers...";
            }
        };
    }

    private void StartPeerDiscoveryRecoveryR198(ManagedTorrent item)
    {
        if (!_settingsR199.FastPeerDiscovery) return;
        if (Interlocked.Exchange(ref item.RecoveryLoopGate, 1) != 0)
        {
            return;
        }

        _ = PeerDiscoveryRecoveryLoopR198(item);
    }

    private async Task PeerDiscoveryRecoveryLoopR198(ManagedTorrent item)
    {
        try
        {
            var attempts = Math.Clamp(_settingsR199.PeerRecoveryAttempts, 1, 60);
            var laterDelay = TimeSpan.FromSeconds(Math.Clamp(_settingsR199.PeerRecoveryIntervalSeconds, 2, 120));
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var delay = attempt switch
                {
                    0 => TimeSpan.FromMilliseconds(200),
                    1 => TimeSpan.FromMilliseconds(900),
                    2 => TimeSpan.FromSeconds(2),
                    _ => laterDelay
                };
                await Task.Delay(delay);
                if (Volatile.Read(ref item.Removed) != 0) return;

                var manager = item.Manager;
                var state = manager.State.ToString();
                if (string.Equals(state, "Stopped", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(state, "Stopping", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(state, "Paused", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(state, "Error", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(state, "Seeding", StringComparison.OrdinalIgnoreCase))
                    return;

                if (manager.OpenConnections > 0 || manager.Monitor.DownloadRate > 0)
                {
                    item.DiscoveryStatus = $"Connected to {manager.OpenConnections} peer(s).";
                    return;
                }

                item.DiscoveryStatus = attempt < 3
                    ? "Fast peer discovery: trackers + DHT + local discovery..."
                    : $"Finding peers — recovery attempt {attempt + 1}/{attempts}...";

                if (_settingsR199.UsePublicTrackerFallback)
                {
                    if (manager.HasMetadata && manager.Torrent is { IsPrivate: false })
                        await AddPublicTrackerFallbacksR199(manager);
                    else if (!manager.HasMetadata && manager.TrackerManager.Tiers.Count == 0)
                        await AddPublicTrackerFallbacksR199(manager);
                }

                // MEDIADOCK_TORRENT_ONE_SHOT_STARTED_ANNOUNCE_R1646
                // Recovery uses normal announces. Re-sending TorrentEvent.Started every few
                // seconds is both unnecessary and can trigger tracker throttling.
                await KickPeerDiscoveryR1910Async(item, sendStartedEvent: false);
            }

            if (item.Manager.OpenConnections == 0 && item.Manager.Monitor.DownloadRate == 0)
                item.DiscoveryStatus = "No reachable peers yet. Open Details to inspect trackers, DHT and connection failures.";
        }
        catch (Exception ex)
        {
            item.LastPeerFailure = FriendlyError(ex);
            WriteCrashLog("PeerDiscoveryRecovery.R1910", ex);
        }
        finally
        {
            Volatile.Write(ref item.RecoveryLoopGate, 0);
        }
    }

    private async Task KickPeerDiscoveryR1910Async(ManagedTorrent item, bool sendStartedEvent)
    {
        async Task TrackerAsync()
        {
            try
            {
                if (item.Manager.TrackerManager.Tiers.Count == 0) return;

                // MEDIADOCK_TORRENT_ONE_SHOT_STARTED_ANNOUNCE_R1646
                var emitStarted = sendStartedEvent &&
                    Interlocked.CompareExchange(ref item.StartedAnnounceGate, 1, 0) == 0;
                if (emitStarted)
                    await item.Manager.TrackerManager.AnnounceAsync(TorrentEvent.Started, CancellationToken.None);
                else
                    await item.Manager.TrackerManager.AnnounceAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                item.LastTrackerStatus = $"Tracker: {FriendlyError(ex)}";
            }
        }

        async Task DhtAsync()
        {
            if (!_settingsR199.EnableDht) return;
            try
            {
                // Do not let a cold UDP/DHT network hold up otherwise useful tracker
                // recovery. Initial startup gets a short bootstrap window; later retries
                // yield quickly because MonoTorrent continues DHT bootstrap in the engine.
                var dhtWaitSeconds = sendStartedEvent ? 4 : 1;
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(dhtWaitSeconds);
                while (_engine.Dht.State != DhtState.Ready && DateTime.UtcNow < deadline)
                {
                    item.DiscoveryStatus = $"Bootstrapping DHT ({_engine.Dht.NodeCount} nodes)...";
                    await Task.Delay(250);
                }

                if (_engine.Dht.State == DhtState.Ready)
                {
                    await item.Manager.DhtAnnounceAsync();
                }
                else
                {
                    item.LastPeerFailure = $"DHT: {_engine.Dht.State} ({_engine.Dht.NodeCount} nodes)";
                }
            }
            catch (Exception ex) { item.LastPeerFailure = $"DHT: {FriendlyError(ex)}"; }
        }

        async Task LocalAsync()
        {
            if (!_settingsR199.EnableLocalPeerDiscovery) return;
            try { await item.Manager.LocalPeerAnnounceAsync(); }
            catch { }
        }

        await Task.WhenAll(TrackerAsync(), DhtAsync(), LocalAsync());
    }

    private static readonly string[] PublicTrackerFallbacksR199 =
    {
        // HTTPS first gives restricted/corporate/mobile networks a discovery path
        // without waiting for UDP tracker timeouts. UDP tiers remain for normal networks.
        "https://tracker.opentrackr.org:443/announce",
        "https://tracker.tamersunion.org:443/announce",
        "https://tracker.gbitt.info:443/announce",
        "udp://tracker.opentrackr.org:1337/announce",
        "udp://tracker.openbittorrent.com:80/announce",
        "udp://open.stealth.si:80/announce",
        "udp://tracker.torrent.eu.org:451/announce",
        "udp://exodus.desync.com:6969/announce",
        "udp://tracker.dler.org:6969/announce",
        "udp://tracker-udp.gbitt.info:80/announce"
    };

    private static async Task AddPublicTrackerFallbacksR199(TorrentManager manager)
    {
        var existing = manager.TrackerManager.Tiers
            .Select(t => t.ActiveTracker?.ToString() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tracker in PublicTrackerFallbacksR199)
        {
            if (existing.Contains(tracker)) continue;
            try { await manager.TrackerManager.AddTrackerAsync(new Uri(tracker)); } catch { }
        }
    }

    private async Task KickInitialPeerDiscoveryR1651Async(ManagedTorrent item)
    {
        if (!_settingsR199.FastPeerDiscovery) return;
        try
        {
            // Await enough of the first discovery cycle to guarantee that tracker/DHT/LPD
            // work was issued, but never let a slow public tracker block Add for too long.
            await KickPeerDiscoveryR1910Async(item, sendStartedEvent: true)
                .WaitAsync(TimeSpan.FromSeconds(6));
        }
        catch (TimeoutException)
        {
            item.DiscoveryStatus = "Peer discovery started and is continuing in the background...";
        }
    }

    private async Task StartAndSettleAsync(ManagedTorrent item)
    {
        var manager = item.Manager;
        var before = manager.State.ToString();
        if (string.Equals(before, "Downloading", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(before, "Seeding", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(before, "Metadata", StringComparison.OrdinalIgnoreCase))
        {
            await KickInitialPeerDiscoveryR1651Async(item);
            return;
        }

        await manager.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var deadline = DateTime.UtcNow + StartSettleTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var state = manager.State.ToString();
            if (string.Equals(state, "Error", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The torrent engine entered an error state while starting.");
            }

            if (!string.Equals(state, "Starting", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(state, "Stopping", StringComparison.OrdinalIgnoreCase))
            {
                // MEDIADOCK_FIRST_LOAD_DISCOVERY_SETTLE_R1651
                // Do not return from a fresh Add/Start before the first tracker/DHT/local
                // discovery kick has actually been issued. Restore already benefited from
                // a warm host; this makes the first load equally deterministic.
                await KickInitialPeerDiscoveryR1651Async(item);
                return;
            }

            await Task.Delay(120);
        }

        throw new TimeoutException($"Torrent start did not settle within {StartSettleTimeout.TotalSeconds:0} seconds. State={manager.State}.");
    }

    private static async Task SafeStopAsync(TorrentManager manager)
    {
        var state = manager.State.ToString();
        if (string.Equals(state, "Stopped", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(state, "Stopping", StringComparison.OrdinalIgnoreCase))
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                if (string.Equals(manager.State.ToString(), "Stopped", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                await Task.Delay(100);
            }
            throw new TimeoutException("Torrent did not finish stopping.");
        }

        await manager.StopAsync(StopTimeout).WaitAsync(TimeSpan.FromSeconds(12));
    }

    private static async Task TryStopManagerAsync(TorrentManager manager)
    {
        try
        {
            await SafeStopAsync(manager);
        }
        catch
        {
            // Cleanup is best effort. The worker process can be restarted without closing MediaDock.
        }
    }

    private static void ResetLiveProgressBaselineR1646(ManagedTorrent item)
    {
        item.LiveProgressBaselineDownloaded = 0;
        item.LiveProgressBaselineVerified = 0;
        Volatile.Write(ref item.LiveProgressBaselineGate, 0);
    }

    private static void SchedulePeerDetailRefreshR1646(ManagedTorrent item)
    {
        var now = Environment.TickCount64;
        var last = Volatile.Read(ref item.LastPeerDetailRefreshTick);
        if (last != 0 && now - last < 1000) return;
        if (Interlocked.Exchange(ref item.PeerDetailRefreshGate, 1) != 0) return;
        Volatile.Write(ref item.LastPeerDetailRefreshTick, now);

        _ = Task.Run(async () =>
        {
            try
            {
                var peers = (await item.Manager.GetPeersAsync()).ToArray();
                Volatile.Write(ref item.CachedConnectedPeers, peers.Length);
                Volatile.Write(ref item.CachedConnectedSeeds, peers.Count(peer => peer.IsSeeder));
                Volatile.Write(ref item.CachedPeerDownloadRate, peers.Sum(peer => Math.Max(0L, peer.Monitor.DownloadRate)));
                Volatile.Write(ref item.CachedPeerUploadRate, peers.Sum(peer => Math.Max(0L, peer.Monitor.UploadRate)));
            }
            catch
            {
                // Seed detail is optional telemetry and must never stall status IPC.
            }
            finally
            {
                Volatile.Write(ref item.PeerDetailRefreshGate, 0);
            }
        });
    }

    private static int GetTrackerSeedCountR1646(TorrentManager manager)
    {
        var seeds = 0;
        try
        {
            foreach (var tier in manager.TrackerManager.Tiers)
            {
                foreach (var info in tier.ScrapeInfo.Values)
                {
                    seeds = Math.Max(seeds, info.Complete);
                }
            }
        }
        catch
        {
            // A tracker may not support scrape; connected-seed telemetry remains available.
        }
        return seeds;
    }

    private static void ScheduleTrackerScrapeR1646(ManagedTorrent item)
    {
        if (!item.Manager.HasMetadata || item.Manager.TrackerManager.Tiers.Count == 0) return;
        var now = Environment.TickCount64;
        var last = Volatile.Read(ref item.LastTrackerScrapeAttemptTick);
        if (last != 0 && now - last < 10000) return;
        if (Interlocked.Exchange(ref item.TrackerScrapeGate, 1) != 0) return;
        Volatile.Write(ref item.LastTrackerScrapeAttemptTick, now);

        _ = Task.Run(async () =>
        {
            try
            {
                // MonoTorrent itself enforces tracker scrape/update intervals.
                await item.Manager.TrackerManager.ScrapeAsync(CancellationToken.None);
            }
            catch
            {
                // Tracker scrape is optional and never blocks transfer/status refresh.
            }
            finally
            {
                Volatile.Write(ref item.TrackerScrapeGate, 0);
            }
        });
    }

    private static long CalculateByteDeltaRateR1647(long currentBytes, long previousBytes, long elapsedMilliseconds)
    {
        if (elapsedMilliseconds <= 0) return 0;
        var delta = currentBytes >= previousBytes
            ? currentBytes - previousBytes
            : currentBytes;
        if (delta <= 0) return 0;
        return Math.Max(0L, (long)Math.Round(delta * 1000d / elapsedMilliseconds));
    }

    // MEDIADOCK_TORRENT_MEASURED_RATE_R1647
    // MEDIADOCK_TORRENT_TRUE_LIVE_RATE_R1654
    // Once two byte-counter samples exist, DataBytesReceived/DataBytesSent are the source
    // of truth. MonoTorrent/per-peer monitor rates are used only for the very first frame.
    // This prevents a stale monitor value from pinning the UI at one apparently static rate.
    private static long SmoothLiveRateR1654(long previousRate, long instantaneousRate)
    {
        if (instantaneousRate <= 0) return 0;
        if (previousRate <= 0) return instantaneousRate;
        return Math.Max(0L, (long)Math.Round(previousRate * 0.25d + instantaneousRate * 0.75d));
    }

    private static (long DownloadRate, long UploadRate) MeasureTransferRatesR1647(
        ManagedTorrent item,
        long downloaded,
        long uploaded,
        long monitorDownloadRate,
        long monitorUploadRate)
    {
        const long minimumSampleMilliseconds = 250;
        const long displayGraceMilliseconds = 700;
        const long staleRateMilliseconds = 1600;

        var nowTick = Environment.TickCount64;
        var peerDownloadRate = Math.Max(0L, Volatile.Read(ref item.CachedPeerDownloadRate));
        var peerUploadRate = Math.Max(0L, Volatile.Read(ref item.CachedPeerUploadRate));

        if (item.RateSampleTick == 0)
        {
            item.RateSampleDownloaded = downloaded;
            item.RateSampleUploaded = uploaded;
            item.RateSampleTick = nowTick;

            // First frame only: show the engine/peer hint while waiting for the first
            // real byte-delta interval. Do not treat this hint as byte activity.
            item.MeasuredDownloadRate = Math.Max(Math.Max(0L, monitorDownloadRate), peerDownloadRate);
            item.MeasuredUploadRate = Math.Max(Math.Max(0L, monitorUploadRate), peerUploadRate);
            return (item.MeasuredDownloadRate, item.MeasuredUploadRate);
        }

        var elapsedMilliseconds = Math.Max(0L, nowTick - item.RateSampleTick);
        if (elapsedMilliseconds >= minimumSampleMilliseconds)
        {
            var downloadedDelta = downloaded >= item.RateSampleDownloaded
                ? downloaded - item.RateSampleDownloaded
                : downloaded;
            var uploadedDelta = uploaded >= item.RateSampleUploaded
                ? uploaded - item.RateSampleUploaded
                : uploaded;

            var instantaneousDownloadRate = CalculateByteDeltaRateR1647(
                downloaded,
                item.RateSampleDownloaded,
                elapsedMilliseconds);
            var instantaneousUploadRate = CalculateByteDeltaRateR1647(
                uploaded,
                item.RateSampleUploaded,
                elapsedMilliseconds);

            if (downloadedDelta > 0)
            {
                item.MeasuredDownloadRate = item.LastDownloadActivityTick == 0
                    ? instantaneousDownloadRate
                    : SmoothLiveRateR1654(item.MeasuredDownloadRate, instantaneousDownloadRate);
                item.LastDownloadActivityTick = nowTick;
            }
            else if (item.LastDownloadActivityTick == 0 || nowTick - item.LastDownloadActivityTick > displayGraceMilliseconds)
            {
                item.MeasuredDownloadRate = 0;
            }

            if (uploadedDelta > 0)
            {
                item.MeasuredUploadRate = item.LastUploadActivityTick == 0
                    ? instantaneousUploadRate
                    : SmoothLiveRateR1654(item.MeasuredUploadRate, instantaneousUploadRate);
                item.LastUploadActivityTick = nowTick;
            }
            else if (item.LastUploadActivityTick == 0 || nowTick - item.LastUploadActivityTick > displayGraceMilliseconds)
            {
                item.MeasuredUploadRate = 0;
            }

            item.RateSampleDownloaded = downloaded;
            item.RateSampleUploaded = uploaded;
            item.RateSampleTick = nowTick;
        }

        if (item.LastDownloadActivityTick != 0 && nowTick - item.LastDownloadActivityTick > staleRateMilliseconds)
            item.MeasuredDownloadRate = 0;
        if (item.LastUploadActivityTick != 0 && nowTick - item.LastUploadActivityTick > staleRateMilliseconds)
            item.MeasuredUploadRate = 0;

        return (Math.Max(0L, item.MeasuredDownloadRate), Math.Max(0L, item.MeasuredUploadRate));
    }

    private Task<object> BuildSnapshotAsync(ManagedTorrent item)
    {
        var manager = item.Manager;
        var verifiedProgress = manager.HasMetadata ? manager.PartialProgress : manager.Progress;
        var totalSize = manager.HasMetadata ? manager.Files.Sum(file => file.Length) : 0L;
        var downloadTargetSize = manager.HasMetadata
            ? manager.Files.Where(file => file.Priority != Priority.DoNotDownload).Sum(file => file.Length)
            : totalSize;
        var monitorDownloadRate = manager.Monitor.DownloadRate;
        var monitorUploadRate = manager.Monitor.UploadRate;
        var downloaded = manager.Monitor.DataBytesReceived;
        var uploaded = manager.Monitor.DataBytesSent;

        // Refresh peer detail asynchronously. The status request itself never waits on
        // GetPeersAsync, but the latest peer count and per-peer rates are folded into
        // the next live snapshot when available.
        SchedulePeerDetailRefreshR1646(item);
        ScheduleTrackerScrapeR1646(item);
        var peers = Math.Max(manager.OpenConnections, Volatile.Read(ref item.CachedConnectedPeers));
        var measuredRates = MeasureTransferRatesR1647(
            item,
            downloaded,
            uploaded,
            monitorDownloadRate,
            monitorUploadRate);
        var downloadRate = measuredRates.DownloadRate;
        var uploadRate = measuredRates.UploadRate;

        // MEDIADOCK_TORRENT_LIVE_PROGRESS_R1646
        // Verified piece progress can advance only when a full piece hashes successfully.
        // DataBytesReceived advances for each torrent-file PieceMessage payload. Blend them
        // so the UI moves while a piece is in flight, but never claim 100% before verified
        // piece completion reaches 100%.
        if (Interlocked.CompareExchange(ref item.LiveProgressBaselineGate, 1, 0) == 0)
        {
            item.LiveProgressBaselineDownloaded = downloaded;
            item.LiveProgressBaselineVerified = verifiedProgress;
        }

        var baselineVerifiedBytes = downloadTargetSize > 0
            ? downloadTargetSize * Math.Clamp(item.LiveProgressBaselineVerified, 0d, 100d) / 100d
            : 0d;
        var payloadBytesSinceBaseline = Math.Max(0L, downloaded - item.LiveProgressBaselineDownloaded);
        var payloadProgress = downloadTargetSize > 0
            ? Math.Min(99.9d, (baselineVerifiedBytes + payloadBytesSinceBaseline) * 100d / downloadTargetSize)
            : 0d;
        var progress = verifiedProgress >= 100d
            ? 100d
            : Math.Max(verifiedProgress, payloadProgress);

        var now = DateTimeOffset.UtcNow;
        if (downloadRate > 0 ||
            downloaded > item.LastSnapshotDownloaded ||
            verifiedProgress > item.LastSnapshotVerifiedProgress)
        {
            item.LastTransferUtc = now;
        }

        item.LastSnapshotDownloaded = Math.Max(item.LastSnapshotDownloaded, downloaded);
        item.LastSnapshotVerifiedProgress = Math.Max(item.LastSnapshotVerifiedProgress, verifiedProgress);
        var liveTransferActive = item.LastTransferUtc != DateTimeOffset.MinValue &&
                                 now - item.LastTransferUtc <= TimeSpan.FromSeconds(5);
        // High-frequency UI snapshots stay cheap. Peer enumeration and tracker scrape
        // are already scheduled above and never block this IPC response.
        var seeds = Math.Max(
            Volatile.Read(ref item.CachedConnectedSeeds),
            GetTrackerSeedCountR1646(manager));

        var eta = -1d;
        if (downloadTargetSize > 0 && downloadRate > 0 && progress < 100)
        {
            var remaining = Math.Max(0d, downloadTargetSize * (1d - progress / 100d));
            eta = remaining / downloadRate;
        }

        return Task.FromResult<object>(new
        {
            Id = item.Id,
            Source = item.Source,
            PersistentSource = item.PersistentSource,
            SavePath = item.SavePath,
            Name = SafeManagerName(manager),
            Status = !string.IsNullOrWhiteSpace(item.LastError)
                ? "Error"
                : manager.State == TorrentState.Downloading && progress < 100
                    ? liveTransferActive || downloadRate > 0 || peers > 0
                        ? "Downloading"
                        : Volatile.Read(ref item.DiscoveredPeers) > 0
                            ? "Connecting peers"
                            : "Finding peers"
                    : manager.State.ToString(),
            Progress = progress,
            VerifiedProgress = verifiedProgress,
            LiveTransferActive = liveTransferActive,
            TotalSize = totalSize,
            DownloadRate = downloadRate,
            UploadRate = uploadRate,
            Peers = peers,
            Seeds = seeds,
            EtaSeconds = eta,
            Downloaded = downloaded,
            Uploaded = uploaded,
            Ratio = downloaded <= 0 ? 0d : uploaded / (double)downloaded,
            LastError = item.LastError,
            DiscoveryStatus = item.DiscoveryStatus,
            LastTrackerStatus = item.LastTrackerStatus,
            LastPeerFailure = item.LastPeerFailure,
            DiscoveredPeers = Volatile.Read(ref item.DiscoveredPeers),
            TrackerPeersDiscovered = Volatile.Read(ref item.TrackerPeersDiscovered),
            DhtPeersDiscovered = Volatile.Read(ref item.DhtPeersDiscovered),
            PexPeersDiscovered = Volatile.Read(ref item.PexPeersDiscovered),
            LocalPeersDiscovered = Volatile.Read(ref item.LocalPeersDiscovered),
            OtherPeersDiscovered = Volatile.Read(ref item.OtherPeersDiscovered),
            ConnectionFailures = Volatile.Read(ref item.ConnectionFailures),
            PeerListenerConfigured = _engine.Settings.ListenEndPoints.Count > 0,
            DhtState = _engine.Dht.State.ToString(),
            DhtNodes = _engine.Dht.NodeCount,
            TrackerCount = manager.TrackerManager.Tiers.Count,
            EngineVersion = "MonoTorrent 3.9 alpha",
            StreamingAvailable = manager.StreamProvider is not null,
            HasMetadata = manager.HasMetadata,
            AddedUtc = item.AddedUtc
        });
    }

    private ManagedTorrent RequiredManaged(JsonElement payload)
    {
        var id = RequiredString(payload, "TorrentId");
        if (!_managed.TryGetValue(id, out var item))
        {
            throw new InvalidOperationException("The torrent job is no longer active in the isolated torrent engine. Add it again.");
        }
        return item;
    }

    private static async Task ValidateTorrentEnvelopeAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length < 8 || stream.Length > MaxTorrentMetadataBytes)
        {
            throw new InvalidDataException("The .torrent file has an invalid metadata size.");
        }

        var first = new byte[1];
        if (await stream.ReadAsync(first.AsMemory(), token) != 1 || first[0] != (byte)'d')
        {
            throw new InvalidDataException("The selected file is not valid BitTorrent metadata (dictionary envelope missing).");
        }

        stream.Seek(-1, SeekOrigin.End);
        var last = new byte[1];
        if (await stream.ReadAsync(last.AsMemory(), token) != 1 || last[0] != (byte)'e')
        {
            throw new InvalidDataException("The selected .torrent metadata is incomplete or malformed.");
        }
    }

    private async Task<string> ResolveTorrentFileAsync(string source, CancellationToken token)
    {
        if (File.Exists(source))
        {
            var fullPath = Path.GetFullPath(source);
            var file = new FileInfo(fullPath);
            if (!file.Extension.Equals(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The selected file is not a .torrent file.");
            }
            if (file.Length < 8 || file.Length > MaxTorrentMetadataBytes)
            {
                throw new InvalidDataException("The .torrent file has an invalid metadata size.");
            }
            return fullPath;
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !uri.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("Choose an existing .torrent file or use a valid magnet/http(s) .torrent URL.", source);
        }

        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long contentLength &&
            (contentLength < 8 || contentLength > MaxTorrentMetadataBytes))
        {
            throw new InvalidDataException("Remote torrent metadata has an invalid size.");
        }

        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), token);
            if (read == 0)
            {
                break;
            }
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            if (output.Length > MaxTorrentMetadataBytes)
            {
                throw new InvalidDataException("Remote torrent metadata is too large.");
            }
        }

        if (output.Length < 8)
        {
            throw new InvalidDataException("Remote torrent metadata is empty or incomplete.");
        }

        var path = Path.Combine(_metadataDirectory, $"{Guid.NewGuid():N}.torrent");
        await File.WriteAllBytesAsync(path, output.ToArray(), token);
        return path;
    }

    private static Priority ParsePriority(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "lowest" => Priority.Lowest,
            "low" => Priority.Low,
            "high" => Priority.High,
            "highest" => Priority.Highest,
            "immediate" => Priority.Immediate,
            "donotdownload" or "do not download" => Priority.DoNotDownload,
            _ => Priority.Normal
        };
    }

    private static string PriorityName(Priority value) => value switch
    {
        Priority.Lowest => "Lowest",
        Priority.Low => "Low",
        Priority.High => "High",
        Priority.Highest => "Highest",
        Priority.Immediate => "Immediate",
        Priority.DoNotDownload => "DoNotDownload",
        _ => "Normal"
    };

    private static bool IsLikelyMedia(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".avi", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".flac", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".wav", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeManagerName(TorrentManager manager)
    {
        try
        {
            var name = manager.Name;
            return string.IsNullOrWhiteSpace(name) ? "Torrent" : name;
        }
        catch
        {
            return "Torrent";
        }
    }

    private static string RequiredString(JsonElement payload, string property)
    {
        if (!TryGetProperty(payload, property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Required torrent field '{property}' was missing.");
        }
        return value.GetString() ?? string.Empty;
    }

    private static int RequiredInt(JsonElement payload, string property)
    {
        if (!TryGetProperty(payload, property, out var value) || !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException($"Required torrent field '{property}' was invalid.");
        }
        return result;
    }

    private static bool OptionalBool(JsonElement payload, string property, bool fallback)
    {
        if (!TryGetProperty(payload, property, out var value) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            return fallback;
        }
        return value.GetBoolean();
    }

    private static string? OptionalString(JsonElement payload, string property)
    {
        return TryGetProperty(payload, property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool TryGetProperty(JsonElement payload, string property, out JsonElement value)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in payload.EnumerateObject())
            {
                if (string.Equals(candidate.Name, property, StringComparison.OrdinalIgnoreCase))
                {
                    value = candidate.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeGate) != 0)
        {
            throw new ObjectDisposedException(nameof(TorrentHostRuntime));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeGate, 1) != 0)
        {
            return;
        }

        foreach (var item in _managed.Values.ToArray())
        {
            await TryStopManagerAsync(item.Manager);
        }
        _managed.Clear();
        _prepared.Clear();

        try
        {
            _engine.Dispose();
        }
        catch (Exception ex)
        {
            WriteCrashLog("Dispose.Engine", ex);
        }
        _http.Dispose();
        _commandGate.Dispose();
    }

    public static string FriendlyError(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null &&
               (current is AggregateException || current is InvalidOperationException))
        {
            current = current.InnerException;
        }

        return current switch
        {
            FileNotFoundException => current.Message,
            UnauthorizedAccessException => "MediaDock cannot access the selected torrent file or download folder.",
            InvalidDataException => current.Message,
            TimeoutException => current.Message,
            OperationCanceledException => "Torrent operation was canceled.",
            _ => $"Torrent engine error: {current.Message}"
        };
    }

    public static void WriteCrashLog(string stage, Exception ex)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AJCoder",
                "MediaDock",
                "Logs");
            Directory.CreateDirectory(directory);
            var text =
                $"MediaDock isolated torrent host crash/error report{Environment.NewLine}" +
                $"Timestamp: {DateTimeOffset.Now:O}{Environment.NewLine}" +
                $"Stage: {stage}{Environment.NewLine}" +
                $"ProcessId: {Environment.ProcessId}{Environment.NewLine}{Environment.NewLine}" +
                ex;
            File.WriteAllText(Path.Combine(directory, "TorrentHost-Last-Crash.txt"), text, Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, $"TorrentHost-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log"), text, Encoding.UTF8);
        }
        catch
        {
        }
    }

    public static async Task RunSelfTestAsync()
    {
        // MEDIADOCK_TORRENT_MEASURED_RATE_SELFTEST_R1647
        // 716,800 bytes over 700 ms is exactly 1,024,000 B/s. This catches
        // regressions where the UI falls back to a permanently-zero monitor rate.
        var measuredRateSelfTest = CalculateByteDeltaRateR1647(716_800, 0, 700);
        if (measuredRateSelfTest != 1_024_000)
        {
            throw new InvalidOperationException($"Measured torrent rate self-test failed: {measuredRateSelfTest} B/s");
        }
        var smoothedRateSelfTestR1654 = SmoothLiveRateR1654(1_000_000, 2_000_000);
        if (smoothedRateSelfTestR1654 != 1_750_000)
        {
            throw new InvalidOperationException($"Live torrent smoothing self-test failed: {smoothedRateSelfTestR1654} B/s");
        }

        var root = Path.Combine(Path.GetTempPath(), $"MediaDock-TorrentHostSelfTest-{Guid.NewGuid():N}");
        var download = Path.Combine(root, "download");
        Directory.CreateDirectory(download);
        var torrentPath = Path.Combine(root, "saved-valid.torrent");
        await File.WriteAllBytesAsync(torrentPath, BuildSavedTorrentSelfTestBytes());
        Environment.SetEnvironmentVariable("MEDIADOCK_TORRENT_SELFTEST_METADATA_DIR", Path.Combine(root, "metadata"));

        await using var runtime = new TorrentHostRuntime();
        try
        {
            var prepare = await runtime.HandleAsync(new TorrentHostRequest
            {
                RequestId = "prepare",
                Command = "prepare",
                Payload = JsonSerializer.SerializeToElement(new { Source = torrentPath })
            });
            if (!prepare.Ok)
            {
                throw new InvalidOperationException("Saved .torrent prepare self-test failed: " + prepare.Error);
            }

            var preview = JsonSerializer.SerializeToElement(prepare.Data);
            var previewId = preview.GetProperty("PreviewId").GetString()!;
            var persistentSource = preview.GetProperty("PersistentSource").GetString() ?? string.Empty;
            if (!File.Exists(persistentSource) ||
                !string.Equals(Path.GetFullPath(Path.GetDirectoryName(persistentSource) ?? string.Empty),
                    Path.GetFullPath(runtime._metadataDirectory), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Canonical .torrent metadata persistence self-test failed.");
            }

            var add = await runtime.HandleAsync(new TorrentHostRequest
            {
                RequestId = "add",
                Command = "add",
                Payload = JsonSerializer.SerializeToElement(new
                {
                    PreviewId = previewId,
                    SavePath = download,
                    StartImmediately = true,
                    EnableStreaming = true,
                    Files = new[] { new { Index = 0, Selected = true, Priority = "Normal" } }
                })
            });
            if (!add.Ok)
            {
                throw new InvalidOperationException("Torrent add self-test failed: " + add.Error);
            }

            var addData = JsonSerializer.SerializeToElement(add.Data);
            var torrentId = addData.GetProperty("TorrentId").GetString()!;
            var startError = addData.GetProperty("StartError");
            var startedStatus = addData.GetProperty("Snapshot").GetProperty("Status").GetString() ?? string.Empty;
            if ((startError.ValueKind != JsonValueKind.Null && !string.IsNullOrWhiteSpace(startError.GetString())) ||
                string.Equals(startedStatus, "Stopped", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(startedStatus, "Error", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Torrent automatic-start self-test failed. Status={startedStatus}, Error={startError}");
            }

            foreach (var command in new[] { "stop", "remove" })
            {
                var response = await runtime.HandleAsync(new TorrentHostRequest
                {
                    RequestId = command,
                    Command = command,
                    Payload = JsonSerializer.SerializeToElement(new { TorrentId = torrentId })
                });
                if (!response.Ok)
                {
                    throw new InvalidOperationException($"Torrent {command} self-test failed: {response.Error}");
                }
            }

            var invalidPath = Path.Combine(root, "saved-invalid.torrent");
            await File.WriteAllTextAsync(invalidPath, "not a torrent", Encoding.UTF8);
            var invalid = await runtime.HandleAsync(new TorrentHostRequest
            {
                RequestId = "invalid",
                Command = "prepare",
                Payload = JsonSerializer.SerializeToElement(new { Source = invalidPath })
            });
            if (invalid.Ok)
            {
                throw new InvalidOperationException("Malformed .torrent self-test was not rejected.");
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

    private static byte[] BuildSavedTorrentSelfTestBytes()
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
