namespace Shortener.BackendShared.Contracts;

public sealed record GetUrlCountRequest(string Domain);

public sealed record GetUrlCountResponse
{
    public long ShardId { get; init; }

    public int Count { get; init; }
}
