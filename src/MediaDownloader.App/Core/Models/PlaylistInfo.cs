namespace MediaDownloader.Core.Models;

public sealed record PlaylistInfo(
    string Id,
    string Title,
    string WebpageUrl,
    string Extractor,
    string Uploader,
    string ThumbnailUrl,
    IReadOnlyList<PlaylistEntryInfo> Entries);
