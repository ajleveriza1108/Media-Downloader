namespace MediaDownloader.Core.Models;

public sealed record PlaylistEntryInfo(
    int Index,
    string Id,
    string Title,
    string WebpageUrl,
    string ThumbnailUrl);
