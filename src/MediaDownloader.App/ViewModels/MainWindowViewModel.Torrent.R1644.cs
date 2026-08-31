using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MediaDownloader.Core.Services;
using MonoTorrent.Client;

namespace MediaDownloader.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly TorrentClientR1644 _torrentClientR1644 = new();
    private readonly DispatcherTimer _torrentRefreshTimerR1644 = new() { Interval = TimeSpan.FromSeconds(1) };
    private string _torrentSourceR1644 = string.Empty;
    private string _torrentStatusR1644 = "Drop a .torrent file or paste a magnet link.";

    public ObservableCollection<TorrentItemR1644> TorrentsR1644 { get; } = [];
    public string TorrentSourceR1644 { get => _torrentSourceR1644; set => SetProperty(ref _torrentSourceR1644, value); }
    public string TorrentStatusR1644 { get => _torrentStatusR1644; private set => SetProperty(ref _torrentStatusR1644, value); }
    public string TorrentOutputDirectoryR1644 => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "MediaDock", "Torrents");

    private void InitializeTorrentR1644()
    {
        Directory.CreateDirectory(TorrentOutputDirectoryR1644);
        _torrentRefreshTimerR1644.Tick += (_, _) =>
        {
            foreach (var item in TorrentsR1644) item.Refresh();
        };
        _torrentRefreshTimerR1644.Start();
    }

    public async Task<TorrentItemR1644> AddTorrentR1644Async(string source, bool streaming)
    {
        if (!TorrentClientR1644.IsTorrentSourceR1644(source))
            throw new InvalidOperationException("Enter a magnet link or choose a .torrent file.");

        TorrentStatusR1644 = streaming ? "Preparing torrent stream..." : "Adding torrent...";
        var manager = await _torrentClientR1644.AddAsync(source, TorrentOutputDirectoryR1644, streaming, CancellationToken.None);
        var item = new TorrentItemR1644
        {
            Source = source,
            SavePath = TorrentOutputDirectoryR1644,
            StreamingMode = streaming,
            Manager = manager,
            Name = string.IsNullOrWhiteSpace(manager.Name) ? "Torrent" : manager.Name
        };
        TorrentsR1644.Insert(0, item);
        await TorrentClientR1644.StartAsync(manager);
        item.Refresh();
        TorrentStatusR1644 = streaming ? "Torrent stream is buffering." : "Torrent started.";
        return item;
    }

    public Task StartTorrentR1644Async(TorrentItemR1644 item) => item.Manager is null ? Task.CompletedTask : TorrentClientR1644.StartAsync(item.Manager);
    public Task PauseTorrentR1644Async(TorrentItemR1644 item) => item.Manager is null ? Task.CompletedTask : TorrentClientR1644.PauseAsync(item.Manager);
    public Task StopTorrentR1644Async(TorrentItemR1644 item) => item.Manager is null ? Task.CompletedTask : TorrentClientR1644.StopAsync(item.Manager);
    public Task RecheckTorrentR1644Async(TorrentItemR1644 item) => item.Manager is null ? Task.CompletedTask : TorrentClientR1644.RecheckAsync(item.Manager);

    public async Task<string?> CreateTorrentStreamUrlR1644Async(TorrentItemR1644 item)
    {
        if (item.Manager is null) return null;
        TorrentStatusR1644 = "Buffering selected torrent media...";
        var url = await _torrentClientR1644.CreateStreamingUrlAsync(item.Manager, CancellationToken.None);
        TorrentStatusR1644 = url is null ? "No streamable file was found." : "Torrent stream ready.";
        return url;
    }

    public IReadOnlyList<TorrentFileChoiceR1644> GetTorrentFilesR1644(TorrentItemR1644 item) =>
        item.Manager is null ? Array.Empty<TorrentFileChoiceR1644>() : TorrentClientR1644.GetFilesR1644(item.Manager);

    public async Task ApplyTorrentFileChoicesR1644Async(TorrentItemR1644 item, IEnumerable<TorrentFileChoiceR1644> choices)
    {
        if (item.Manager is null) return;
        await TorrentClientR1644.ApplyFileChoicesR1644Async(item.Manager, choices);
    }

    public async Task DisposeTorrentR1644Async()
    {
        _torrentRefreshTimerR1644.Stop();
        await _torrentClientR1644.DisposeAsync();
    }
}
