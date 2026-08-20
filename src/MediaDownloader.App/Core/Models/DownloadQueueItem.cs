using MediaDownloader.Infrastructure;

namespace MediaDownloader.Core.Models;

public sealed class DownloadQueueItem : ObservableObject
{
    private double _progressPercent;
    private string _status = "Queued";
    private string _progressText = string.Empty;
    private string _speedText = string.Empty;
    private string _outputPath = string.Empty;
    private bool _completed;

    public DownloadQueueItem(string title, string source, string quality, string format, string thumbnailUrl)
    {
        Title = title;
        Source = source;
        Quality = quality;
        Format = format;
        ThumbnailUrl = thumbnailUrl;
    }

    public string Title { get; }
    public string Source { get; }
    public string Quality { get; }
    public string Format { get; }
    public string ThumbnailUrl { get; }

    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
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
        set => SetProperty(ref _outputPath, value);
    }

    public bool Completed
    {
        get => _completed;
        set => SetProperty(ref _completed, value);
    }
}
