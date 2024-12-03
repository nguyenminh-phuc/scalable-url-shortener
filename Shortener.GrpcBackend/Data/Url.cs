using NodaTime;

namespace Shortener.GrpcBackend.Data;

public sealed class Url
{
    public int Id { get; init; }

    public string DestinationUrl { get; init; } = null!;

    public int UserId { get; init; }

    public User User { get; init; } = null!;

    public int DomainId { get; init; }

    public Domain Domain { get; init; } = null!;

    public int TotalViews { get; init; }

    public IList<Visit> Visits { get; init; } = [];

    public Instant CreatedAt { get; init; }

    public Instant UpdatedAt { get; init; }
}
