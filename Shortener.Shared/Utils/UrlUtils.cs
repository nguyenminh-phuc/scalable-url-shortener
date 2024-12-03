using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Shortener.Shared.Utils;

public partial class UrlUtils
{
    // https://stackoverflow.com/a/52772923
    public static bool IsValidHttpUrl(string s, [NotNullWhen(true)] out Uri? resultUri)
    {
        if (!UrlRegex().IsMatch(s))
        {
            s = $"https://{s}";
        }

        if (Uri.TryCreate(s, UriKind.Absolute, out resultUri))
        {
            return resultUri.Scheme == Uri.UriSchemeHttp ||
                   resultUri.Scheme == Uri.UriSchemeHttps;
        }

        return false;
    }

    [GeneratedRegex(@"^https?:\/\/", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}
