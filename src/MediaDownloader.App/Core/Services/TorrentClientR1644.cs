using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;

namespace MediaDownloader.Core.Services;

public sealed class TorrentItemR1644 : INotifyPropertyChanged
{
    private string _name = "Torrent";
    private string _status = "Queued";
    private double _progress;
    private string _downloadRate = "0 KB/s";
    private string _uploadRate = "0 KB/s";
    private string _peers = "0 peers";

    internal TorrentManager? Manager { get; set; }
    public string Source { get; init; } = string.Empty;
    public string SavePath { get; init; } = string.Empty;
    public bool StreamingMode { get; init; }

    public string Name { get => _name; internal set => Set(ref _name, value); }
    public string Status { get => _status; internal set => Set(ref _status, value); }
    public double Progress { get => _progress; internal set => Set(ref _progress, value); }
    public string DownloadRate { get => _downloadRate; internal set => Set(ref _downloadRate, value); }
    public string UploadRate { get => _uploadRate; internal set => Set(ref _uploadRate, value); }
    public string Peers { get => _peers; internal set => Set(ref _peers, value); }
    public bool CanStart => Manager is not null;
    public bool CanPause => Manager is not null;
    public bool CanStop => Manager is not null;
    public bool CanRecheck => Manager is not null && Manager.HasMetadata;
    public bool CanStream => StreamingMode && Manager?.StreamProvider is not null;

    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Refresh()
    {
        var manager = Manager;
        if (manager is null) return;
        Name = string.IsNullOrWhiteSpace(manager.Name) ? Name : manager.Name;
        Status = manager.State.ToString();
        Progress = manager.Progress;
        DownloadRate = FormatRate(manager.Monitor.DownloadRate);
        UploadRate = FormatRate(manager.Monitor.UploadRate);
        Peers = $"{manager.OpenConnections} peers";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStart)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanPause)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStop)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRecheck)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStream)));
    }

    private static string FormatRate(long bytesPerSecond)
    {
        if (bytesPerSecond >= 1024L * 1024L)
            return $"{bytesPerSecond / (1024d * 1024d):0.0} MB/s";
        return $"{bytesPerSecond / 1024d:0.0} KB/s";
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class TorrentFileChoiceR1644 : INotifyPropertyChanged
{
    private bool _selected = true;
    private Priority _priority = Priority.Normal;
    internal ITorrentManagerFile File { get; }
    public string Path => File.Path;
    public long Length => File.Length;
    public string SizeText => FormatSize(File.Length);
    public bool Selected { get => _selected; set { if (_selected == value) return; _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); } }
    public Priority Priority { get => _priority; set { if (_priority == value) return; _priority = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Priority))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal TorrentFileChoiceR1644(ITorrentManagerFile file) { File = file; _selected = file.Priority != Priority.DoNotDownload; _priority = file.Priority; }
    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L) return $"{bytes / (1024d * 1024d * 1024d):0.00} GB";
        if (bytes >= 1024L * 1024L) return $"{bytes / (1024d * 1024d):0.0} MB";
        return $"{bytes / 1024d:0.0} KB";
    }
}

public sealed class TorrentClientR1644 : IAsyncDisposable
{
    public const int StreamingPortR1644 = 55126;
    private readonly ClientEngine _engine;
    private readonly HttpClient _httpClient = new(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });
    private readonly string _metadataDownloadDirectory;

    public TorrentClientR1644()
    {
        _metadataDownloadDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AJCoder", "MediaDock", "TorrentMetadata");
        Directory.CreateDirectory(_metadataDownloadDirectory);

        var settings = new EngineSettingsBuilder
        {
            AllowPortForwarding = true,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                ["ipv4"] = new(IPAddress.Any, 0),
                ["ipv6"] = new(IPAddress.IPv6Any, 0)
            },
            DhtEndPoint = new IPEndPoint(IPAddress.Any, 0),
            HttpStreamingPrefix = $"http://127.0.0.1:{StreamingPortR1644}/"
        };
        _engine = new ClientEngine(settings.ToSettings());
    }

    public static bool IsTorrentSourceR1644(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        var value = source.Trim();
        if (value.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase)) return MagnetLink.TryParse(value, out _);
        if (value.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               uri.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<TorrentManager> AddAsync(string source, string savePath, bool streaming, CancellationToken token)
    {
        Directory.CreateDirectory(savePath);
        var settings = new TorrentSettingsBuilder
        {
            MaximumConnections = 250,
            UploadSlots = 8,
            MaximumDownloadRate = 0,
            MaximumUploadRate = 0,
            AllowPeerExchange = true
        }.ToSettings();

        if (MagnetLink.TryParse(source.Trim(), out var magnet))
        {
            return streaming
                ? await _engine.AddStreamingAsync(magnet, savePath)
                : await _engine.AddAsync(magnet, savePath, settings);
        }

        var torrentPath = await ResolveTorrentFileAsync(source, token);
        if (streaming)
        {
            var torrent = await Torrent.LoadAsync(torrentPath);
            return await _engine.AddStreamingAsync(torrent, savePath);
        }

        return await _engine.AddAsync(torrentPath, savePath, settings);
    }

    public async Task<string?> CreateStreamingUrlAsync(TorrentManager manager, CancellationToken token)
    {
        await manager.StartAsync();
        await manager.WaitForMetadataAsync(token);
        var file = manager.Files
            .Where(f => IsLikelyMedia(f.Path))
            .OrderByDescending(f => f.Length)
            .FirstOrDefault() ?? manager.Files.OrderByDescending(f => f.Length).FirstOrDefault();
        if (file is null || manager.StreamProvider is null) return null;
        var stream = await manager.StreamProvider.CreateHttpStreamAsync(file, token);
        return $"http://127.0.0.1:{StreamingPortR1644}{stream.RelativeUri}";
    }

    public static IReadOnlyList<TorrentFileChoiceR1644> GetFilesR1644(TorrentManager manager) =>
        manager.Files.Select(file => new TorrentFileChoiceR1644(file)).ToArray();

    public static async Task ApplyFileChoicesR1644Async(TorrentManager manager, IEnumerable<TorrentFileChoiceR1644> choices)
    {
        foreach (var choice in choices)
        {
            var priority = choice.Selected ? choice.Priority : Priority.DoNotDownload;
            await manager.SetFilePriorityAsync(choice.File, priority);
        }
    }

    public static Task StartAsync(TorrentManager manager) => manager.StartAsync();
    public static Task PauseAsync(TorrentManager manager) => manager.PauseAsync();
    public static Task StopAsync(TorrentManager manager) => manager.StopAsync();

    public static async Task RecheckAsync(TorrentManager manager)
    {
        if (manager.State != TorrentState.Stopped)
            await manager.StopAsync();
        await manager.HashCheckAsync(false);
    }

    private async Task<string> ResolveTorrentFileAsync(string source, CancellationToken token)
    {
        if (File.Exists(source)) return Path.GetFullPath(source);
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Choose a .torrent file or enter a valid magnet/http(s) torrent URL.");

        var bytes = await _httpClient.GetByteArrayAsync(uri, token);
        if (bytes.Length < 8 || bytes.Length > 20 * 1024 * 1024)
            throw new InvalidDataException("Torrent metadata response has an invalid size.");
        var path = Path.Combine(_metadataDownloadDirectory, $"{Guid.NewGuid():N}.torrent");
        await File.WriteAllBytesAsync(path, bytes, token);
        return path;
    }

    private static bool IsLikelyMedia(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".avi", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".m4v", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".m4a", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".flac", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".wav", StringComparison.OrdinalIgnoreCase);
    }

    public static void RunSelfTestR1644()
    {
        if (!IsTorrentSourceR1644("magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567") ||
            !IsTorrentSourceR1644("https://example.com/file.torrent") ||
            IsTorrentSourceR1644("https://example.com/file.zip"))
            throw new InvalidOperationException("R1.6.44 torrent source classifier contract failed.");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var manager in _engine.Torrents.ToArray())
        {
            try { await manager.StopAsync(); } catch { }
        }
        _engine.Dispose();
        _httpClient.Dispose();
    }
}
