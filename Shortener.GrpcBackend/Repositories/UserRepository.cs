using Microsoft.EntityFrameworkCore;
using Shortener.GrpcBackend.Data;

namespace Shortener.GrpcBackend.Repositories;

public interface IUserRepository
{
    Task<User> Add(string username, string hashedPassword, CancellationToken cancellationToken);

    Task<User?> Get(string username, CancellationToken cancellationToken);

    Task<bool> Update(int id, string hashedPassword, CancellationToken cancellationToken);

    Task<bool> Delete(int id, CancellationToken cancellationToken);
}

public sealed class UserRepository(BackendDbContext context) : IUserRepository
{
    public async Task<User> Add(string username, string hashedPassword, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             INSERT INTO "User" ("Username", "HashedPassword", "CreatedAt", "UpdatedAt")
             VALUES ({username}, {hashedPassword}, NOW(), NOW())
             RETURNING *
             """;
        User user = (await context.Users.FromSql(query).ToListAsync(cancellationToken)).First();

        return user;
    }

    public async Task<User?> Get(string username, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             SELECT * FROM "User" WHERE "Username" = {username}
             """;
        User? user = await context.Users.FromSql(query).SingleOrDefaultAsync(cancellationToken);

        return user;
    }

    public async Task<bool> Update(int id, string hashedPassword, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             UPDATE "User"
             SET "HashedPassword" = {hashedPassword}, "UpdatedAt" = NOW()
             WHERE "Id" = {id}
             """;
        int rowsAffected = await context.Database.ExecuteSqlAsync(query, cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<bool> Delete(int id, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             DELETE FROM "User" WHERE "Id" = {id}
             """;
        int rowsAffected = await context.Database.ExecuteSqlAsync(query, cancellationToken);

        return rowsAffected > 0;
    }
}
