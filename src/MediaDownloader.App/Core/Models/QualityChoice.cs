namespace MediaDownloader.Core.Models;

public enum QualityChoiceKind
{
    Best,
    ExactHeight,
    AudioOnly
}

public sealed record QualityChoice(
    QualityChoiceKind Kind,
    int? Height,
    string Label,
    int? Fps = null,
    string? FormatId = null,
    bool FormatHasAudio = false)
{
    public override string ToString() => Label;
}
