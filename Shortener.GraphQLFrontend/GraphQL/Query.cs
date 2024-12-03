using FairyBread;
using Shortener.FrontendShared.Dtos;
using Shortener.FrontendShared.Validators;

namespace Shortener.GraphQLFrontend.GraphQL;

public sealed class Query
{
    public async Task<UrlStats> GetUrl(
        [Validate(typeof(ShortUrlValidator))] string shortUrl, UrlDataLoader urlDataLoader) =>
        await urlDataLoader.LoadAsync(shortUrl);
}
