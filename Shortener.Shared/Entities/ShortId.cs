using Shortener.Shared.Services;

namespace Shortener.Shared.Entities;

public sealed record ShortId(long Range, int Index)
{
    public override string ToString()
    {
        long base10Id = UrlEncoder.RangeSize * Range + Index + UrlEncoder.StarterRange;
        return UrlEncoder.Base10ToBase62(base10Id);
    }

    public string ToString(string scheme, string host) => $"{scheme}://{host}/{ToString()}";
}
