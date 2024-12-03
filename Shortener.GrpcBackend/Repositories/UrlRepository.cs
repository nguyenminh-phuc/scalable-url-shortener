using Microsoft.EntityFrameworkCore;
using NodaTime;
using Npgsql;
using Shortener.GrpcBackend.Data;
using Url = Shortener.GrpcBackend.Data.Url;

namespace Shortener.GrpcBackend.Repositories;

public interface IUrlRepository
{
    Task<int> GetCount(int domainId, CancellationToken cancellationToken);

    Task<int?> GetMaxId(CancellationToken cancellationToken = default);

    Task<Url> Add(int userId, string destinationUrl, int domainId, CancellationToken cancellationToken);

    Task<Url?> GetByUrlId(int id, bool includeVisits, CancellationToken cancellationToken);

    Task<IList<Url>?> GetByUrlIds(IList<int> ids, CancellationToken cancellationToken);

    Task<IList<Url>?> GetByUserId(
        int userId,
        int limit, (int, Instant)? cursor, bool latestFirst,
        CancellationToken cancellationToken);

    Task<bool> Update(int id, int userId, int domainId, string destinationUrl, CancellationToken cancellationToken);

    Task<bool> IncrementVisit(int id, CancellationToken cancellationToken);

    Task<bool> Delete(int id, int userId, CancellationToken cancellationToken);
}

public sealed class UrlRepository(BackendDbContext context) : IUrlRepository
{
    public async Task<int> GetCount(int domainId, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             SELECT COUNT(*) AS "Value" FROM "Url"
             WHERE "DomainId" = {domainId}
             """;
        int count = await context.Database.SqlQuery<int>(query).SingleOrDefaultAsync(cancellationToken);

        return count;
    }

    public async Task<int?> GetMaxId(CancellationToken cancellationToken = default)
    {
        FormattableString query =
            $"""
             SELECT MAX("Id") AS "Value" FROM "Url"
             """;
        int? max = await context.Database.SqlQuery<int?>(query).SingleOrDefaultAsync(cancellationToken);

        return max;
    }

    public async Task<Url> Add(int userId, string destinationUrl, int domainId,
        CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             INSERT INTO "Url" ("UserId", "DomainId", "DestinationUrl", "CreatedAt", "UpdatedAt")
             VALUES ({userId}, {domainId}, {destinationUrl}, NOW(), NOW())
             RETURNING *
             """;
        Url url = (await context.Urls.FromSql(query).ToListAsync(cancellationToken)).First();

        return url;
    }

    public async Task<Url?> GetByUrlId(int id, bool includeVisits, CancellationToken cancellationToken)
    {
        // Note: The SQL query cannot include JOIN queries to get related data.
        // Use Include method to load related entities after FromSql() method.
        // https://learn.microsoft.com/en-us/ef/core/querying/sql-queries?tabs=sqlserver#limitations
        FormattableString query =
            $"""
             SELECT * FROM "Url" WHERE "Id" = {id}
             """;

        IQueryable<Url> urlQuery = context.Urls.FromSql(query);
        if (includeVisits)
        {
            urlQuery = urlQuery.Include(u => u.Visits);
        }

        return await urlQuery.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IList<Url>?> GetByUrlIds(IList<int> ids, CancellationToken cancellationToken)
    {
        string[] parameterNames = ids.Select((_, i) => $"@id{i}").ToArray();
        List<NpgsqlParameter<int>> parameters = ids
            .Select((id, i) => new NpgsqlParameter<int>($"@id{i}", id))
            .ToList();

        string query =
            $"""
             SELECT * FROM "Url" WHERE "Id" IN ({string.Join(", ", parameterNames)})
             """;

        List<Url> urls = await context.Urls.FromSqlRaw(query, parameters)
            .Include(u => u.Visits)
            .ToListAsync(cancellationToken);

        return urls.Count == 0 ? null : urls;
    }

    public async Task<IList<Url>?> GetByUserId(
        int userId,
        int limit, (int, Instant)? cursor, bool latestFirst,
        CancellationToken cancellationToken)
    {
        FormattableString query;
        if (cursor.HasValue)
        {
            (int urlIdCursor, Instant updatedAtCursor) = cursor.Value;

            query = latestFirst
                ? (FormattableString)
                $"""
                 SELECT * FROM "Url"
                 WHERE "UserId" = {userId} AND
                       ("UpdatedAt" < {updatedAtCursor} OR ("UpdatedAt" = {updatedAtCursor} AND "Id" < {urlIdCursor}))
                 ORDER BY "UpdatedAt" DESC, "Id" DESC
                 LIMIT {limit}
                 """
                : (FormattableString)
                $"""
                 SELECT * FROM "Url"
                 WHERE "UserId" = {userId} AND
                      ("UpdatedAt" > {updatedAtCursor} OR ("UpdatedAt" = {updatedAtCursor} AND "Id" > {urlIdCursor}))
                 ORDER BY "UpdatedAt" ASC, "Id" ASC
                 LIMIT {limit}
                 """;
        }
        else
        {
            query = latestFirst
                ? (FormattableString)
                $"""
                 SELECT * FROM "Url"
                 WHERE "UserId" = {userId}
                 ORDER BY "UpdatedAt" DESC, "Id" DESC
                 LIMIT {limit}
                 """
                : (FormattableString)
                $"""
                 SELECT * FROM "Url"
                 WHERE "UserId" = {userId}
                 ORDER BY "UpdatedAt" ASC, "Id" ASC
                 LIMIT {limit}
                 """;
        }

        List<Url> urls = await context.Urls.FromSql(query).Include(u => u.Visits).ToListAsync(cancellationToken);

        return urls.Count == 0 ? null : urls;
    }

    public async Task<bool> Update(
        int id, int userId, int domainId, string destinationUrl, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             UPDATE "Url"
             SET "DestinationUrl" = {destinationUrl}, "DomainId" = {domainId}, "UpdatedAt" = NOW()
             WHERE "Id" = {id} AND "UserId" = {userId}
             """;
        int rowsAffected = await context.Database.ExecuteSqlAsync(query, cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<bool> IncrementVisit(int id, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             UPDATE "Url"
             SET "TotalViews" = "TotalViews" + 1, "UpdatedAt" = NOW()
             WHERE "Id" = {id}
             """;
        int rowsAffected = await context.Database.ExecuteSqlAsync(query, cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<bool> Delete(int id, int userId, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             DELETE FROM "Url" WHERE "Id" = {id} AND "UserId" = {userId}
             """;
        int rowsAffected = await context.Database.ExecuteSqlAsync(query, cancellationToken);

        return rowsAffected > 0;
    }
}
