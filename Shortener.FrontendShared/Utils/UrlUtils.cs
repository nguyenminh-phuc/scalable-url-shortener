using Shortener.Shared.Entities;
using Shortener.Shared.Services;
using UrlStats = Shortener.FrontendShared.Dtos.UrlStats;
using SharedUrlStats = Shortener.Shared.Entities.UrlStats;

namespace Shortener.FrontendShared.Utils;

public static class UrlUtils
{
    public static Connection<UrlStats> Convert(Connection<SharedUrlStats> stats, IShortUrlService shortUrlService) =>
        new()
        {
            PageInfo = stats.PageInfo,
            Edges = stats.Edges.Select(edge => new Edge<UrlStats>
            {
                Cursor = edge.Cursor, Node = new UrlStats(edge.Node, shortUrlService)
            }).ToList()
        };
}
