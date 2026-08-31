using System.Text;
using System.Text.Json;

namespace MediaDownloader;

public enum BrowserHandlerModeR1643
{
    Analyze,
    Download
}

public enum BrowserHandlerKindR1643
{
    Page,
    File
}

public sealed record BrowserHandlerRequestR1643(
    int Version,
    BrowserHandlerModeR1643 Mode,
    BrowserHandlerKindR1643 Kind,
    string Url,
    string Title,
    string FileName,
    string MimeType,
    string Referrer,
    long ContentLength,
    string Source)
{
    public const string CommandLineSwitch = "--browser-handler-r1643";
    private const int MaxEncodedLength = 65536;
    private const int MaxUrlLength = 16384;

    public static BrowserHandlerRequestR1643? TryParseCommandLine(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], CommandLineSwitch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return TryParseEncoded(args[i + 1], out var request) ? request : null;
        }

        return null;
    }

    public static bool TryParseEncoded(string? encoded, out BrowserHandlerRequestR1643? request)
    {
        request = null;
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaxEncodedLength)
        {
            return false;
        }

        try
        {
            var bytes = Base64UrlDecode(encoded);
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;

            var version = root.TryGetProperty("version", out var versionNode) &&
                          versionNode.TryGetInt32(out var parsedVersion)
                ? parsedVersion
                : 0;
            var modeText = ReadString(root, "mode");
            var kindText = ReadString(root, "kind");
            var url = ReadString(root, "url");

            if (version != 2 || !TryNormalizeBrowserUrl(url, out var normalizedUrl))
            {
                return false;
            }

            var referrer = ReadString(root, "referrer");
            if (!string.IsNullOrWhiteSpace(referrer))
            {
                referrer = TryNormalizeHttpUrl(referrer, out var normalizedReferrer)
                    ? normalizedReferrer
                    : string.Empty;
            }

            var contentLength = root.TryGetProperty("contentLength", out var lengthNode) &&
                                lengthNode.TryGetInt64(out var parsedLength) &&
                                parsedLength > 0
                ? parsedLength
                : 0;

            request = new BrowserHandlerRequestR1643(
                2,
                string.Equals(modeText, "analyze", StringComparison.OrdinalIgnoreCase)
                    ? BrowserHandlerModeR1643.Analyze
                    : BrowserHandlerModeR1643.Download,
                string.Equals(kindText, "page", StringComparison.OrdinalIgnoreCase)
                    ? BrowserHandlerKindR1643.Page
                    : BrowserHandlerKindR1643.File,
                normalizedUrl,
                Truncate(ReadString(root, "title"), 512),
                Truncate(ReadString(root, "fileName"), 260),
                Truncate(ReadString(root, "mimeType"), 160),
                referrer,
                contentLength,
                Truncate(ReadString(root, "source"), 128));
            return true;
        }
        catch
        {
            return false;
        }
    }


    internal static bool TryNormalizeBrowserUrl(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxUrlLength) return false;
        var trimmed = value.Trim();
        if (trimmed.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) && MonoTorrent.MagnetLink.TryParse(trimmed, out _))
        {
            normalized = trimmed;
            return true;
        }
        return TryNormalizeHttpUrl(trimmed, out normalized);
    }

    internal static bool TryNormalizeHttpUrl(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxUrlLength)
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        normalized = uri.AbsoluteUri;
        return true;
    }

    public static void RunSelfTestR1643()
    {
        const string json =
            "{\"version\":2,\"mode\":\"download\",\"kind\":\"file\",\"url\":\"https://example.com/archive.7z\",\"title\":\"Fixture\",\"fileName\":\"archive.7z\",\"mimeType\":\"application/x-7z-compressed\",\"referrer\":\"https://example.com/\",\"contentLength\":1234,\"source\":\"self-test\"}";

        var encoded = Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        if (!TryParseEncoded(encoded, out var parsed) ||
            parsed is null ||
            parsed.Mode != BrowserHandlerModeR1643.Download ||
            parsed.Kind != BrowserHandlerKindR1643.File ||
            !string.Equals(parsed.Url, "https://example.com/archive.7z", StringComparison.Ordinal) ||
            parsed.ContentLength != 1234)
        {
            throw new InvalidOperationException("R1.6.43 browser-handler payload contract failed.");
        }

        const string badJson =
            "{\"version\":2,\"mode\":\"download\",\"kind\":\"file\",\"url\":\"file:///C:/Windows/win.ini\"}";

        if (TryParseEncoded(Base64UrlEncode(Encoding.UTF8.GetBytes(badJson)), out _))
        {
            throw new InvalidOperationException("R1.6.43 browser-handler URL safety contract failed.");
        }
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var node) && node.ValueKind == JsonValueKind.String
            ? node.GetString() ?? string.Empty
            : string.Empty;

    private static string Truncate(string? value, int maximum)
    {
        var text = value?.Trim() ?? string.Empty;
        return text[..Math.Min(text.Length, maximum)];
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += (normalized.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Invalid base64url payload.")
        };

        return Convert.FromBase64String(normalized);
    }
}
