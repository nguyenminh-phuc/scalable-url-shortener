using Shortener.GraphQLFrontend.Middleware;

namespace Shortener.GraphQLFrontend.GraphQL;

public sealed class MutationType(IConfiguration configuration) : ObjectType<Mutation>
{
    protected override void Configure(IObjectTypeDescriptor<Mutation> descriptor)
    {
        descriptor.Authorize();

        descriptor
            .Field(x => x.CreateUser(default!, default!))
            .UseRateLimiter(configuration, RateLimitType.Restricted)
            .AllowAnonymous();

        descriptor
            .Field(x => x.GenerateToken(default!, default!, default!))
            .UseRateLimiter(configuration, RateLimitType.Restricted)
            .AllowAnonymous();

        descriptor
            .Field(x => x.UpdateUser(default!, default!, default!))
            .UseRateLimiter(configuration, RateLimitType.Restricted);

        descriptor
            .Field(x => x.DeleteUser(default!, default!))
            .UseRateLimiter(configuration, RateLimitType.Restricted);

        descriptor
            .Field(x => x.CreateUrl(default!, default!, default!))
            .UseRateLimiter(configuration, RateLimitType.Normal);

        descriptor
            .Field(x => x.UpdateUrl(default!, default!, default!))
            .UseRateLimiter(configuration, RateLimitType.Normal);

        descriptor
            .Field(x => x.DeleteUrl(default!, default!, default!))
            .UseRateLimiter(configuration, RateLimitType.Normal);
    }
}
