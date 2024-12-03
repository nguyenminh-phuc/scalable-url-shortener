namespace Shortener.Admin.Dtos;

public sealed class UrlCounts
{
    public required string Domain { get; init; }

    public Dictionary<long, int?> Counts { get; init; } = [];
}
