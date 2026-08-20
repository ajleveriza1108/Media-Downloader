namespace MediaDownloader.Core.Models;

public sealed record MediaAnalysisResult(
    MediaInfo? Media,
    PlaylistInfo? Playlist,
    string Diagnostics)
{
    public bool IsPlaylist => Playlist is not null;
}
