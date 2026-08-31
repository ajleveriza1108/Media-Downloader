using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaDownloader;
using MediaDownloader.Core.Services;

namespace MediaDownloader.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const int MaxConcurrentGeneralDownloadsR1643 = 4;
    private readonly GeneralDownloadServiceR1643 _generalDownloadServiceR1643 = new();
    private readonly GeneralDownloadStoreR1643 _generalDownloadStoreR1643 = new();
    private readonly SemaphoreSlim _generalDownloadSlotsR1643 =
        new(MaxConcurrentGeneralDownloadsR1643, MaxConcurrentGeneralDownloadsR1643);
    private string _generalDownloadUrlR1643 = string.Empty;

    public ObservableCollection<GeneralDownloadItemR1643> GeneralDownloadsR1643 { get; } = [];

    public string GeneralDownloadUrlR1643
    {
        get => _generalDownloadUrlR1643;
        set => SetProperty(ref _generalDownloadUrlR1643, value);
    }

    public string GeneralDownloadOutputDirectoryR1643 =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "MediaDock",
            "Files");

    public string GeneralDownloadSummaryR1643
    {
        get
        {
            var active = GeneralDownloadsR1643.Count(item =>
                item.State is GeneralDownloadStateR1643.Queued or GeneralDownloadStateR1643.Downloading);
            var completed = GeneralDownloadsR1643.Count(item =>
                item.State == GeneralDownloadStateR1643.Completed);

            return $"{GeneralDownloadsR1643.Count} item(s) - {active} active - {completed} completed";
        }
    }

    private void InitializeGeneralDownloaderR1643()
    {
        GeneralDownloadsR1643.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(GeneralDownloadSummaryR1643));
        };

        if (!_queuePersistenceEnabled)
        {
            return;
        }

        foreach (var item in _generalDownloadStoreR1643.Load())
        {
            AttachGeneralDownloadItemR1643(item);
            GeneralDownloadsR1643.Add(item);
        }

        OnPropertyChanged(nameof(GeneralDownloadSummaryR1643));
    }

    public async Task QueueGeneralDownloadFromTextR1643Async()
    {
        var raw = GeneralDownloadUrlR1643.Trim();
        if (!BrowserHandlerRequestR1643.TryNormalizeHttpUrl(raw, out var normalized))
        {
            Status = "Paste a valid http or https file URL.";
            return;
        }

        var request = new BrowserHandlerRequestR1643(
            2,
            BrowserHandlerModeR1643.Download,
            BrowserHandlerKindR1643.File,
            normalized,
            "Direct file",
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            "MediaDock Downloader");

        await QueueGeneralDownloadR1643Async(request, autoStart: true);
    }

    public async Task QueueGeneralDownloadR1643Async(
        BrowserHandlerRequestR1643 request,
        bool autoStart,
        string? outputDirectory = null,
        string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!BrowserHandlerRequestR1643.TryNormalizeHttpUrl(request.Url, out var normalizedUrl))
        {
            Status = "The browser supplied an invalid download URL.";
            return;
        }

        var activeDuplicate = GeneralDownloadsR1643.FirstOrDefault(item =>
            string.Equals(item.Url, normalizedUrl, StringComparison.OrdinalIgnoreCase) &&
            (item.State is GeneralDownloadStateR1643.Queued or GeneralDownloadStateR1643.Downloading));

        if (activeDuplicate is not null)
        {
            Status = $"Already downloading: {activeDuplicate.FileName}";
            return;
        }

        var item = new GeneralDownloadItemR1643
        {
            Url = normalizedUrl,
            FileName = string.IsNullOrWhiteSpace(fileName)
                ? GeneralDownloadClassifierR1643.ResolveSuggestedFileNameR1643(request)
                : GeneralDownloadClassifierR1643.SanitizeFileNameR1643(fileName),
            OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                ? GeneralDownloadOutputDirectoryR1643
                : outputDirectory,
            Referrer = request.Referrer,
            MimeType = request.MimeType,
            Source = request.Source,
            TotalBytes = request.ContentLength,
            StatusText = string.IsNullOrWhiteSpace(request.Source)
                ? "Ready"
                : $"Received from {request.Source}",
            State = GeneralDownloadStateR1643.Ready
        };

        AttachGeneralDownloadItemR1643(item);
        GeneralDownloadsR1643.Insert(0, item);
        GeneralDownloadUrlR1643 = string.Empty;
        SaveGeneralDownloadsR1643();
        Status = $"Added to Downloader: {item.FileName}";

        if (autoStart)
        {
            await StartGeneralDownloadR1643Async(item);
        }
    }

    public async Task StartGeneralDownloadR1643Async(GeneralDownloadItemR1643 item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.CanStartR1643)
        {
            return;
        }

        item.ActiveCancellationR1643?.Cancel();
        item.ActiveCancellationR1643?.Dispose();
        item.ActiveCancellationR1643 = new CancellationTokenSource();
        var cancellationToken = item.ActiveCancellationR1643.Token;

        item.State = GeneralDownloadStateR1643.Queued;
        item.StatusText = "Queued";
        SaveGeneralDownloadsR1643();

        var slotEntered = false;

        try
        {
            await _generalDownloadSlotsR1643.WaitAsync(cancellationToken);
            slotEntered = true;

            var outputDirectory = string.IsNullOrWhiteSpace(item.OutputDirectory)
                ? GeneralDownloadOutputDirectoryR1643
                : item.OutputDirectory;

            await _generalDownloadServiceR1643.DownloadAsync(
                item,
                outputDirectory,
                cancellationToken);

            Status = $"Completed: {item.FileName}";
        }
        catch (OperationCanceledException)
        {
            item.State = GeneralDownloadStateR1643.Paused;
            item.StatusText = "Paused - resume available";
            item.SpeedText = string.Empty;
            Status = $"Paused: {item.FileName}";
        }
        catch (Exception ex)
        {
            item.State = GeneralDownloadStateR1643.Failed;
            item.StatusText = "Failed - click Start / Resume to retry";
            item.SpeedText = string.Empty;
            Status = $"Download failed: {item.FileName}";
            App.WriteCrashLog("GeneralDownloader.R1643", ex);
        }
        finally
        {
            if (slotEntered)
            {
                _generalDownloadSlotsR1643.Release();
            }

            item.ActiveCancellationR1643?.Dispose();
            item.ActiveCancellationR1643 = null;
            SaveGeneralDownloadsR1643();
            OnPropertyChanged(nameof(GeneralDownloadSummaryR1643));
        }
    }

    public void CancelGeneralDownloadR1643(GeneralDownloadItemR1643 item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.ActiveCancellationR1643?.Cancel();
    }

    public Task OpenGeneralDownloadR1643Async(GeneralDownloadItemR1643 item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.CanOpenR1643)
        {
            Status = "The downloaded file is not available.";
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.OutputPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Status = "Could not open the downloaded file.";
            App.WriteCrashLog("GeneralDownloader.Open.R1643", ex);
        }

        return Task.CompletedTask;
    }

    public Task OpenGeneralDownloadFolderR1643Async()
    {
        try
        {
            Directory.CreateDirectory(GeneralDownloadOutputDirectoryR1643);
            Process.Start(new ProcessStartInfo
            {
                FileName = GeneralDownloadOutputDirectoryR1643,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Status = "Could not open the Downloader folder.";
            App.WriteCrashLog("GeneralDownloader.OpenFolder.R1643", ex);
        }

        return Task.CompletedTask;
    }

    private void AttachGeneralDownloadItemR1643(GeneralDownloadItemR1643 item)
    {
        item.PropertyChanged -= GeneralDownloadItemPropertyChangedR1643;
        item.PropertyChanged += GeneralDownloadItemPropertyChangedR1643;
    }

    private void GeneralDownloadItemPropertyChangedR1643(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GeneralDownloadItemR1643.State)
            or nameof(GeneralDownloadItemR1643.StatusText))
        {
            OnPropertyChanged(nameof(GeneralDownloadSummaryR1643));
        }
    }

    private void SaveGeneralDownloadsR1643()
    {
        if (_queuePersistenceEnabled)
        {
            _generalDownloadStoreR1643.Save(GeneralDownloadsR1643);
        }
    }
}
