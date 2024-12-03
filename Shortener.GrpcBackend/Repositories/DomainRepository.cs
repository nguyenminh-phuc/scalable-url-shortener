using Microsoft.EntityFrameworkCore;
using Shortener.GrpcBackend.Data;

namespace Shortener.GrpcBackend.Repositories;

public interface IDomainRepository
{
    Task<Domain?> Get(string name, CancellationToken cancellationToken);

    Task<Domain> GetOrAdd(string name, CancellationToken cancellationToken);
}

public sealed class DomainRepository(BackendDbContext context) : IDomainRepository
{
    public async Task<Domain?> Get(string name, CancellationToken cancellationToken)
    {
        name = name.ToLowerInvariant();

        FormattableString query =
            $"""
             SELECT * FROM "Domain" WHERE "Name" = {name}
             """;
        Domain? user = await context.Domains.FromSql(query).SingleOrDefaultAsync(cancellationToken);

        return user;
    }

    public async Task<Domain> GetOrAdd(string name, CancellationToken cancellationToken)
    {
        name = name.ToLowerInvariant();

        // https://stackoverflow.com/a/6722460
        FormattableString query =
            $"""
             WITH new_row AS (
                INSERT INTO "Domain" ("Name", "CreatedAt")
                SELECT {name}, NOW()
                WHERE NOT EXISTS (SELECT * FROM "Domain" WHERE "Name" = {name})
                RETURNING *
             )
             SELECT * FROM new_row
             UNION
             SELECT * FROM "Domain" WHERE "Name" = {name}
             """;
        Domain domain = (await context.Domains.FromSql(query).ToListAsync(cancellationToken)).First();

        return domain;
    }
}
