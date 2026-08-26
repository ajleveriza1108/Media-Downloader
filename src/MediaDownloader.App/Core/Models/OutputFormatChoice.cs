namespace MediaDownloader.Core.Models;

public enum OutputFormatKind
{
    Mp4,
    Mkv,
    Mp3,
    M4a,
    Flac
}

public sealed record OutputFormatChoice(
    OutputFormatKind Kind,
    string Label)
{
    public override string ToString() => Label;
}
