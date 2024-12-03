using FairyBread;
using Shortener.FrontendShared.Dtos;
using Shortener.FrontendShared.Middleware;
using Shortener.FrontendShared.Services;
using Shortener.FrontendShared.Validators;
using Shortener.Shared.Entities;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;

namespace Shortener.GraphQLFrontend.GraphQL;

public sealed class Mutation
{
    public async Task<User> CreateUser(CreateUserInput user, [Service] IUserService userService)
    {
        (long shardId, string token) = await userService.Create(user.Username, user.Password);
        return new User { ShardId = shardId, Username = user.Username, Token = token };
    }

    public async Task<User> GenerateToken(
        GenerateTokenInput user,
        [Service] IShardService shardService,
        [Service] IUserService userService)
    {
        if (!shardService.IsOnline(user.ShardId))
        {
            throw new GraphQLException("Service unavailable");
        }

        string token = await userService.GenerateToken(user.ShardId, user.Username, user.Password);
        return new User { ShardId = user.ShardId, Username = user.Username, Token = token };
    }

    public async Task<bool> UpdateUser(
        [Validate(typeof(PasswordGraphQLValidator))]
        string password,
        [Service] IHttpContextAccessor context, [Service] IUserService userService)
    {
        UserId userId = (UserId)context.HttpContext!.Items[JwtHandler.UserId]!;
        bool updated = await userService.Update(userId, password);

        return updated;
    }

    public async Task<bool> DeleteUser([Service] IHttpContextAccessor context, [Service] IUserService userService)
    {
        UserId userId = (UserId)context.HttpContext!.Items[JwtHandler.UserId]!;
        bool deleted = await userService.Delete(userId);

        return deleted;
    }

    public async Task<UrlMapping?> CreateUrl(
        CreateUrlInput input,
        [Service] IHttpContextAccessor context, [Service] IUrlService urlService)
    {
        UserId userId = (UserId)context.HttpContext!.Items[JwtHandler.UserId]!;

        UrlMapping url = await urlService.Create(userId, input.DestinationUrl);
        return url;
    }

    public async Task<bool> UpdateUrl(
        UpdateUrlInput input,
        [Service] IHttpContextAccessor context, [Service] IUrlService urlService)
    {
        UserId userId = (UserId)context.HttpContext!.Items[JwtHandler.UserId]!;
        ShortId shortId = ShortIdUtils.ParseUrl(input.ShortUrl);
        if (shortId.Range != userId.ShardId)
        {
            throw new GraphQLException("Forbidden");
        }

        bool updated = await urlService.Update(shortId, userId, input.DestinationUrl);

        return updated;
    }

    public async Task<bool> DeleteUrl(
        [Validate(typeof(ShortUrlValidator))] string shortUrl,
        [Service] IHttpContextAccessor context, [Service] IUrlService urlService)
    {
        UserId userId = (UserId)context.HttpContext!.Items[JwtHandler.UserId]!;
        ShortId shortId = ShortIdUtils.ParseUrl(shortUrl);
        if (shortId.Range != userId.ShardId)
        {
            throw new GraphQLException("Forbidden");
        }

        bool deleted = await urlService.Delete(shortId, userId);

        return deleted;
    }
}
