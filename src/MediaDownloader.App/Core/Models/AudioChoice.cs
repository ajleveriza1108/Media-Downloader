namespace MediaDownloader.Core.Models;

public sealed record AudioChoice(
    string Label,
    string? FormatId = null)
{
    public override string ToString() => Label;
}
