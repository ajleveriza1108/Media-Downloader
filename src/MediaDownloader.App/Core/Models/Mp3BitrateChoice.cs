namespace MediaDownloader.Core.Models;

public sealed record Mp3BitrateChoice(
    int KilobitsPerSecond,
    string Label)
{
    public override string ToString() => Label;
}
