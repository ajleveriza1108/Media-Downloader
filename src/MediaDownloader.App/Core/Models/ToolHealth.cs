namespace MediaDownloader.Core.Models;

public sealed record ToolHealth(
    string Name,
    string Path,
    bool Available,
    string Version,
    string Message)
{
    public string Status => Available ? "Ready" : "Missing";
}
