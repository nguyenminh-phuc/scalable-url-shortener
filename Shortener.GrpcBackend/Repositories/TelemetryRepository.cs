using Microsoft.EntityFrameworkCore;
using Shortener.GrpcBackend.Data;

namespace Shortener.GrpcBackend.Repositories;

public sealed record TelemetryCounts(int UserCount, int UrlCount, int DomainCount);

public interface ITelemetryRepository
{
    Task<TelemetryCounts> GetCounts(CancellationToken cancellationToken = default);
}

public sealed class TelemetryRepository(BackendDbContext context) : ITelemetryRepository
{
    public async Task<TelemetryCounts> GetCounts(CancellationToken cancellationToken = default)
    {
        FormattableString query =
            $"""
             SELECT
                (SELECT COUNT(*) FROM "User") AS "UserCount",
                (SELECT COUNT(*) FROM "Url") AS "UrlCount",
                (SELECT COUNT(*) FROM "Domain") AS "DomainCount"
             """;

        return await context.Database.SqlQuery<TelemetryCounts>(query).SingleAsync(cancellationToken);
    }
}
