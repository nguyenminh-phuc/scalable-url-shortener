using System.Net;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

namespace Shortener.GrpcBackend.Services;

public interface IGeoIpService
{
    Task<string?> GetCountry(IPAddress ip);
}

public sealed class GeoIpService : IGeoIpService
{
    private const string DefaultFilePath = "./GeoLite2-Country.mmdb";

    private readonly DatabaseReader _reader;

    public GeoIpService(IConfiguration configuration)
    {
        string file = configuration["GEOIP_PATH"] ?? DefaultFilePath;
        if (!File.Exists(file))
        {
            throw new FileNotFoundException("GEOIP_PATH not found", file);
        }

        _reader = new DatabaseReader(file);
    }

    public Task<string?> GetCountry(IPAddress ip) =>
        _reader.TryCountry(ip, out CountryResponse? response)
            ? Task.FromResult(response!.Country.IsoCode)
            : Task.FromResult<string?>(null);
}
