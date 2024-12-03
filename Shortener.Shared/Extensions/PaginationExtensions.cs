using Google.Protobuf.Collections;
using Shortener.Shared.Entities;
using Shortener.Shared.Grpc;
using UrlStats = Shortener.Shared.Entities.UrlStats;

namespace Shortener.Shared.Extensions;

using GrpcConnection = GetUrlsByUserIdReply.Types.UrlsConnection;
using GrpcEdge = GetUrlsByUserIdReply.Types.UrlsEdge;

public static class PaginationExtensions
{
    public static GrpcConnection Serialize(this Connection<UrlStats> connection)
    {
        RepeatedField<GrpcEdge> edges = [];
        foreach (Edge<UrlStats> edge in connection.Edges)
        {
            edges.Add(edge.Serialize());
        }

        GrpcConnection grpcConnection = new() { PageInfo = connection.PageInfo.Serialize(), Edges = { edges } };

        return grpcConnection;
    }

    private static UrlsPageInfo Serialize(this PageInfo pageInfo)
    {
        UrlsPageInfo grpcPageInfo = new()
        {
            HasNextPage = pageInfo.HasNextPage, HasPreviousPage = pageInfo.HasPreviousPage
        };

        if (pageInfo.StartCursor is not null)
        {
            grpcPageInfo.StartCursor = pageInfo.StartCursor;
        }

        if (pageInfo.EndCursor is not null)
        {
            grpcPageInfo.EndCursor = pageInfo.EndCursor;
        }

        return grpcPageInfo;
    }

    private static GrpcEdge Serialize(this Edge<UrlStats> edge) =>
        new() { Cursor = edge.Cursor, Node = edge.Node.Serialize() };
}
