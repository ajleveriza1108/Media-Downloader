namespace MediaDownloader.Core.Models;

public sealed record MediaInfo(
    string Id,
    string Title,
    string WebpageUrl,
    string Extractor,
    string Uploader,
    double? DurationSeconds,
    string ThumbnailUrl,
    IReadOnlyList<MediaFormat> Formats);
