using NodaTime;

namespace Shortener.GrpcBackend.Data;

public sealed class Domain
{
    public int Id { get; init; }

    public string Name { get; init; } = null!;

    public IList<Url> Urls { get; init; } = [];

    public Instant CreatedAt { get; init; }
}
