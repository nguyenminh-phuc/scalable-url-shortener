namespace Shortener.Shared.Entities;

public sealed class PageInfo
{
    public bool HasNextPage { get; set; }

    public bool HasPreviousPage { get; set; }

    public string? StartCursor { get; set; }

    public string? EndCursor { get; set; }
}

public sealed class Edge<T>
{
    public required string Cursor { get; init; }

    public required T Node { get; init; }
}

public sealed class Connection<T>
{
    public required PageInfo PageInfo { get; init; }

    public IList<Edge<T>> Edges { get; init; } = [];
}
