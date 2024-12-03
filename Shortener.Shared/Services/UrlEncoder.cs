using System.Text;
using System.Text.RegularExpressions;

namespace Shortener.Shared.Services;

public static partial class UrlEncoder
{
    private const string Elements = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public const long RangeSize = 10_000_000;

    public const long StarterRange = 100_000_000_000;

    [GeneratedRegex("^[a-zA-Z0-9]{7}$")]
    public static partial Regex Base62Regex();

    public static bool IsValidBase62(string s) => Base62Regex().IsMatch(s);

    public static long Base62ToBase10(string base62)
    {
        long x = base62.Aggregate<char, long>(0, (current, t) => current * 62 + Convert(t));

        return x;
    }

    public static string Base10ToBase62(long base10)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(base10);

        StringBuilder base62 = new();
        while (base10 != 0)
        {
            base62.Insert(0, Elements[(int)(base10 % 62)]);
            base10 /= 62;
        }

        while (base62.Length != 7)
        {
            base62.Insert(0, '0');
        }

        return base62.ToString();
    }

    private static int Convert(char c) =>
        c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'z' => c - 'a' + 10,
            >= 'A' and <= 'Z' => c - 'A' + 36,
            _ => throw new ArgumentOutOfRangeException(nameof(c), $"Invalid base62 character: {c}")
        };
}
