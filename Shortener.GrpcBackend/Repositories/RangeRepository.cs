using Microsoft.EntityFrameworkCore;
using Shortener.GrpcBackend.Data;
using Range = Shortener.GrpcBackend.Data.Range;

namespace Shortener.GrpcBackend.Repositories;

public interface IRangeRepository
{
    Task<Range> Get(CancellationToken cancellationToken = default);
}

public sealed class RangeRepository(BackendDbContext context) : IRangeRepository
{
    public async Task<Range> Get(CancellationToken cancellationToken = default)
    {
        FormattableString query =
            $"""
             SELECT * FROM "Range"
             """;
        Range range = await context.Ranges.FromSql(query).SingleAsync(cancellationToken);

        return range;
    }
}
