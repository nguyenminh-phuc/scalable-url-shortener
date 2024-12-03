using Microsoft.Extensions.Configuration;
using Shortener.Shared.Entities;
using Shortener.Shared.Utils;

namespace Shortener.Shared.Services;

public interface IShortUrlService
{
    string Get(string shortId);

    string Get(ShortId id);
}

public sealed class ShortUrlService : IShortUrlService
{
    private const string DefaultScheme = "http";

    private readonly string _host;
    private readonly string _scheme;

    public ShortUrlService(IConfiguration configuration)
    {
        _scheme = configuration["REDIRECT_SCHEME"] ?? DefaultScheme;

        string? host = configuration["REDIRECT_HOST"];
        if (string.IsNullOrEmpty(host))
        {
            throw new Exception("REDIRECT_HOST is required");
        }

        _host = host;
    }

    public string Get(string shortId)
    {
        ShortId id = ShortIdUtils.ParseId(shortId);
        return Get(id);
    }

    public string Get(ShortId id) => id.ToString(_scheme, _host);
}
