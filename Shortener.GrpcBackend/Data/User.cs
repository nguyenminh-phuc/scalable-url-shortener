using NodaTime;

namespace Shortener.GrpcBackend.Data;

public sealed class User
{
    public int Id { get; init; }

    public string Username { get; init; } = null!;

    public string HashedPassword { get; init; } = null!;

    public IList<Url> Urls { get; init; } = [];

    public Instant CreatedAt { get; init; }

    public Instant UpdatedAt { get; init; }
}
