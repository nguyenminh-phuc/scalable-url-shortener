using System.Diagnostics.CodeAnalysis;
using NodaTime;
using Shortener.Shared.Entities;
using Shortener.Shared.Services;

namespace Shortener.Shared.Utils;

public static class ShortIdUtils
{
    public static ShortId ParseId(string shortId)
    {
        if (!UrlEncoder.IsValidBase62(shortId))
        {
            throw new ArgumentException($"Invalid shortId: {shortId}", nameof(shortId));
        }

        long base10Id = UrlEncoder.Base62ToBase10(shortId);
        long range = (base10Id - UrlEncoder.StarterRange) / UrlEncoder.RangeSize;
        int index = (int)((base10Id - UrlEncoder.StarterRange) % UrlEncoder.RangeSize);

        return new ShortId(range, index);
    }

    public static bool TryParseUrl(string shortUrl, [NotNullWhen(true)] out ShortId? id)
    {
        id = null;

        if (!UrlUtils.IsValidHttpUrl(shortUrl, out Uri? uri))
        {
            return false;
        }

        string path = uri.AbsolutePath;
        if (!path.StartsWith('/'))
        {
            return false;
        }

        path = path[1..];

        if (!UrlEncoder.IsValidBase62(path))
        {
            return false;
        }

        id = ParseId(path);
        return true;
    }

    public static ShortId ParseUrl(string shortUrl)
    {
        if (!UrlUtils.IsValidHttpUrl(shortUrl, out Uri? uri))
        {
            throw new ArgumentException($"Invalid shortUrl: {shortUrl}", shortUrl);
        }

        string path = uri.AbsolutePath;
        if (!path.StartsWith('/'))
        {
            throw new ArgumentException($"Invalid shortUrl: {shortUrl}", shortUrl);
        }

        path = path[1..];

        if (!UrlEncoder.IsValidBase62(path))
        {
            throw new ArgumentException($"Invalid shortUrl: {shortUrl}", shortUrl);
        }

        return ParseId(path);
    }

    public static string CreateCursor(int urlId, Instant updatedAt)
    {
        string opaque = $"{urlId}:{updatedAt.ToUnixTimeMilliseconds()}";
        return Base64Utils.Encode(opaque);
    }

    public static (int urlId, Instant updatedAt) ParseCursor(string? opaqueCursor)
    {
        if (string.IsNullOrEmpty(opaqueCursor))
        {
            throw new ArgumentException($"Invalid cursor: {opaqueCursor}", nameof(opaqueCursor));
        }

        string[] cursor = Base64Utils.Decode(opaqueCursor).Split(':');
        if (cursor.Length != 2)
        {
            throw new ArgumentException($"Invalid cursor: {opaqueCursor}", nameof(opaqueCursor));
        }

        if (!int.TryParse(cursor[0], out int urlId))
        {
            throw new ArgumentException($"Invalid cursor: {opaqueCursor}", nameof(opaqueCursor));
        }

        if (!long.TryParse(cursor[1], out long updatedAtMs))
        {
            throw new ArgumentException($"Invalid cursor: {opaqueCursor}", nameof(opaqueCursor));
        }

        try
        {
            Instant updatedAt = Instant.FromUnixTimeMilliseconds(updatedAtMs);
            return (urlId, updatedAt);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentException($"Invalid cursor: {opaqueCursor}", nameof(opaqueCursor));
        }
    }
}
