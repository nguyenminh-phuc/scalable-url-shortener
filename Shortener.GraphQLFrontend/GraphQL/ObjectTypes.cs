using Shortener.Shared.Entities;
using UrlStats = Shortener.FrontendShared.Dtos.UrlStats;

namespace Shortener.GraphQLFrontend.GraphQL;

public sealed class UrlStatsType : ObjectType<UrlStats>
{
    protected override void Configure(IObjectTypeDescriptor<UrlStats> descriptor)
    {
        descriptor.Field(x => x.LastDay).Type<NonNullType<ViewStatsType>>();
        descriptor.Field(x => x.LastWeek).Type<NonNullType<ViewStatsType>>();
        descriptor.Field(x => x.LastMonth).Type<NonNullType<ViewStatsType>>();
        descriptor.Field(x => x.AllTime).Type<NonNullType<ViewStatsType>>();
    }
}

public sealed class ViewStatsType : ObjectType<ViewStats>
{
    protected override void Configure(IObjectTypeDescriptor<ViewStats> descriptor) =>
        descriptor.Ignore(x => x.Serialize());
}
