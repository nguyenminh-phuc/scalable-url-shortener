using FluentValidation.Results;
using HotChocolate.Execution;
using Shortener.FrontendShared.Dtos;
using Shortener.FrontendShared.Middleware;
using Shortener.FrontendShared.Services;
using Shortener.FrontendShared.Validators;
using Shortener.GraphQLFrontend.Middleware;
using Shortener.Shared.Entities;
using HcConnection = HotChocolate.Types.Pagination.Connection<Shortener.FrontendShared.Dtos.UrlStats>;
using HcConnectionPageInfo = HotChocolate.Types.Pagination.ConnectionPageInfo;
using HcEdge = HotChocolate.Types.Pagination.Edge<Shortener.FrontendShared.Dtos.UrlStats>;
using UrlStats = Shortener.FrontendShared.Dtos.UrlStats;

namespace Shortener.GraphQLFrontend.GraphQL;

public sealed class QueryType(IConfiguration configuration) : ObjectType<Query>
{
    protected override void Configure(IObjectTypeDescriptor<Query> descriptor)
    {
        descriptor.Authorize();

        descriptor
            .Field(x => x.GetUrl(default!, default!))
            .UseRateLimiter(configuration, RateLimitType.Normal);

        descriptor.Field("urls")
            .UseRateLimiter(configuration, RateLimitType.Normal)
            .UsePaging<UrlStatsType>()
            .Resolve(async context =>
            {
                int? first = context.ArgumentValue<int?>("first");
                string? after = context.ArgumentValue<string?>("after");
                int? last = context.ArgumentValue<int?>("last");
                string? before = context.ArgumentValue<string?>("before");

                GetUrlsValidator validator = new();
                ValidationResult result = await validator.ValidateAsync(new GetUrlsInput
                {
                    First = first, After = after, Last = last, Before = before
                });
                if (!result.IsValid)
                {
                    throw new QueryException(result.ToString());
                }

                IHttpContextAccessor httpContext = context.Service<IHttpContextAccessor>();
                UserId userId = (UserId)httpContext.HttpContext!.Items[JwtHandler.UserId]!;

                IUrlService urlService = context.Service<IUrlService>();

                Connection<UrlStats> connection = await urlService.GetByUserId(userId, first, after, last, before);

                List<HcEdge> edges = connection.Edges.Select(edge => new HcEdge(edge.Node, edge.Cursor)).ToList();

                HcConnectionPageInfo pageInfo = new(
                    connection.PageInfo.HasNextPage,
                    connection.PageInfo.HasPreviousPage,
                    connection.PageInfo.StartCursor,
                    connection.PageInfo.EndCursor);

                HcConnection hcConnection = new(edges, pageInfo);

                return hcConnection;
            });
    }
}
