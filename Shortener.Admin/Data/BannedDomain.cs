using NodaTime;

namespace Shortener.Admin.Data;

public sealed class BannedDomain
{
    public int Id { get; init; }

    public string Name { get; init; } = null!;

    public Instant CreatedAt { get; init; }
}
