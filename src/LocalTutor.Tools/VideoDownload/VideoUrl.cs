using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace LocalTutor.Tools.VideoDownload;

internal static class VideoUrl
{
    internal static IReadOnlyList<string> Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.Any(char.IsWhiteSpace) ||
            value.Any(char.IsControl) || value.Contains('\\') || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            return ["InvalidUrl: one public HTTPS video URL without credentials or a fragment is required."];
        bool supported = uri.Host switch
        {
            "www.youtube.com" or "youtube.com" => uri.AbsolutePath == "/watch" && Regex.IsMatch(uri.Query, @"\A\?v=[A-Za-z0-9_-]{11}\z"),
            "youtu.be" => uri.Query.Length == 0 && Regex.IsMatch(uri.AbsolutePath, @"\A/[A-Za-z0-9_-]{11}\z"),
            "vimeo.com" or "www.vimeo.com" => uri.Query.Length == 0 && Regex.IsMatch(uri.AbsolutePath, @"\A/[0-9]{1,12}\z"),
            // Direct public Commons media only, not an arbitrary generic web-page fetch.
            "upload.wikimedia.org" => uri.Query.Length == 0 && uri.AbsolutePath.StartsWith("/wikipedia/commons/", StringComparison.Ordinal) &&
                Regex.IsMatch(uri.AbsolutePath, @"\.(mp4|webm)\z") && !uri.AbsolutePath.Contains("%", StringComparison.Ordinal),
            _ => false
        };
        return supported ? [] : ["UnsupportedUrl: use a single YouTube watch/short URL, public Vimeo video, or direct Wikimedia Commons MP4/WebM; playlists and other hosts are disabled."];
    }

    internal static async Task CheckNetworkAsync(string url, Func<string, CancellationToken, Task<IPAddress[]>> resolve, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try { addresses = await resolve(new Uri(url).DnsSafeHost, cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
        catch (SocketException) { throw new VideoToolException("NetworkUnavailable: the approved source could not be resolved."); }
        catch (TimeoutException) { throw new VideoToolException("TimedOut: source address resolution exceeded its time budget."); }
        if (addresses.Length == 0 || addresses.Any(a => !IsPublic(a)))
            throw new VideoToolException("BlockedNetwork: local, private and reserved source addresses are denied.");
    }

    internal static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) return IsPublic(address.MapToIPv4());
        byte[] b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 0x01 && b[2] <= 0x01) &&
                !(b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) &&
                !(b[0] == 0x20 && b[1] == 0x02); // Exclude special-use and transition ranges.
        return address.AddressFamily == AddressFamily.InterNetwork && b[0] is not (0 or 10 or 127) && b[0] < 224 &&
            !(b[0] == 100 && b[1] is >= 64 and <= 127) && !(b[0] == 169 && b[1] == 254) &&
            !(b[0] == 172 && b[1] is >= 16 and <= 31) && !(b[0] == 192 && (b[1] == 168 || b[1] == 0 || b[1] == 2)) &&
            !(b[0] == 198 && (b[1] is 18 or 19 || b[1] == 51 && b[2] == 100)) && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }

    internal static string Source(string url) => new Uri(url).Host switch
    {
        "youtu.be" or "youtube.com" or "www.youtube.com" => "YouTube",
        "vimeo.com" or "www.vimeo.com" => "Vimeo",
        _ => "Wikimedia Commons"
    };
}
