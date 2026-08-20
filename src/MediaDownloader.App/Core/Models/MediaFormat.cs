namespace MediaDownloader.Core.Models;

public sealed record MediaFormat(
    string FormatId,
    int? Width,
    int? Height,
    double? Fps,
    string Extension,
    string VideoCodec,
    string AudioCodec,
    string Protocol,
    long? FileSize,
    double? VideoBitrate,
    double? AudioBitrate,
    bool HasVideo,
    bool HasAudio,
    bool IsDrm)
{
    public string DisplayName
    {
        get
        {
            if (!HasVideo && HasAudio)
            {
                return $"Audio only · {Extension.ToUpperInvariant()} · {AudioCodec}";
            }

            var resolution = Height is > 0 ? $"{Height}p" : "Video";
            var fps = Fps is >= 50 ? $" {Math.Round(Fps.Value):0} FPS" : string.Empty;
            var codec = string.IsNullOrWhiteSpace(VideoCodec) ? string.Empty : $" · {VideoCodec}";
            var ext = string.IsNullOrWhiteSpace(Extension) ? string.Empty : $" · {Extension.ToUpperInvariant()}";
            return $"{resolution}{fps}{ext}{codec}";
        }
    }
}
