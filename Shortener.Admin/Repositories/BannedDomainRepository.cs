using Microsoft.EntityFrameworkCore;
using Shortener.Admin.Data;

namespace Shortener.Admin.Repositories;

public interface IBannedDomainRepository
{
    Task<int> GetCount(CancellationToken cancellationToken);

    Task<BannedDomain?> Get(string name, CancellationToken cancellationToken = default);

    Task<bool> Add(string name, CancellationToken cancellationToken = default);

    Task<bool> Delete(string name, CancellationToken cancellationToken = default);
}

public sealed class BannedDomainRepository(AdminDbContext context) : IBannedDomainRepository
{
    public async Task<int> GetCount(CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             SELECT COUNT(*) AS "Value" FROM "BannedDomain"
             """;
        int count = await context.Database.SqlQuery<int>(query).SingleOrDefaultAsync(cancellationToken);

        return count;
    }

    public async Task<BannedDomain?> Get(string name, CancellationToken cancellationToken = default)
    {
        FormattableString query =
            $"""
             SELECT * FROM "BannedDomain" WHERE "Name" = {name}
             """;

        BannedDomain? bannedDomain = await context.BannedDomains.FromSql(query).SingleOrDefaultAsync(cancellationToken);

        return bannedDomain;
    }

    public async Task<bool> Add(string name, CancellationToken cancellationToken = default)
    {
        FormattableString query =
            $"""
             INSERT INTO "BannedDomain" ("Name", "CreatedAt") VALUES ({name}, NOW()) ON CONFLICT("Name") DO NOTHING
             """;
        int rowsAffected = await context.Database.ExecuteSqlAsync(query, cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<bool> Delete(string name, CancellationToken cancellationToken = default)
    {
        FormattableString query =
            $"""
             DELETE FROM "BannedDomain" WHERE "Name" = {name}
             """;
        int rowsAffected = await context.Database.ExecuteSqlAsync(query, cancellationToken);

        return rowsAffected > 0;
    }
}
