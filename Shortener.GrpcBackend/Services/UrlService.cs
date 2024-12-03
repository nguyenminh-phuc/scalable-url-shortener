using System.Net;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MyCSharp.HttpUserAgentParser.Providers;
using NodaTime;
using Shortener.GrpcBackend.Data;
using Shortener.GrpcBackend.Repositories;
using Shortener.Shared;
using Shortener.Shared.Entities;
using Shortener.Shared.Exceptions;
using Shortener.Shared.Extensions;
using Shortener.Shared.Grpc;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;
using BrowserType = Shortener.Shared.Entities.BrowserType;
using CacheUtils = Shortener.BackendShared.Utils.CacheUtils;
using SharedCacheUtils = Shortener.Shared.Utils.CacheUtils;
using Duration = NodaTime.Duration;
using PlatformType = Shortener.Shared.Entities.PlatformType;
using Url = Shortener.GrpcBackend.Data.Url;
using UrlServiceBase = Shortener.Shared.Grpc.UrlService.UrlServiceBase;
using UrlStats = Shortener.Shared.Entities.UrlStats;
using UserAgent = Shortener.Shared.Entities.UserAgent;
using UserAgentType = Shortener.Shared.Entities.UserAgentType;
using ViewStats = Shortener.Shared.Entities.ViewStats;

namespace Shortener.GrpcBackend.Services;

public sealed class UrlService(
    IBackgroundTaskQueue taskQueue,
    IDomainService domainService,
    IGeoIpService geoIpService,
    IShardService shardService,
    IDomainRepository domainRepository,
    IUrlRepository urlRepository)
    : UrlServiceBase
{
    public override async Task<CreateUrlReply> Create(CreateUrlRequest request, ServerCallContext context)
    {
        if (shardService.Type == ShardType.ReadOnly)
        {
            throw RpcExceptionUtils.ResourceExhausted(nameof(ShortId));
        }

        if (!UrlUtils.IsValidHttpUrl(request.DestinationUrl, out Uri? uri))
        {
            throw RpcExceptionUtils.InvalidArgument(nameof(request.DestinationUrl));
        }

        bool banned = await domainService.IsBanned(uri.Host);
        if (banned)
        {
            throw RpcExceptionUtils.InvalidArgument(nameof(request.DestinationUrl));
        }

        Domain domain = await domainRepository.GetOrAdd(uri.Host, context.CancellationToken);

        Url url = await urlRepository.Add(
            request.UserId, request.DestinationUrl, domain.Id,
            context.CancellationToken);

        if (url.Id >= Constants.ThresholdForNewUsers)
        {
            if (shardService.Type == ShardType.ReadWrite)
            {
                await ChangeShardTypeInBackground(ShardType.WriteUrlsOnly);
            }
            else if (url.Id >= Constants.ThresholdForNewUrls)
            {
                await ChangeShardTypeInBackground(ShardType.ReadOnly);
            }
        }

        ShortId shortId = new(shardService.Id, url.Id);

        return new CreateUrlReply
        {
            Url = new GrpcUrlMapping { ShortId = shortId.ToString(), DestinationUrl = request.DestinationUrl }
        };
    }

    public override async Task<RedirectReply> Redirect(RedirectRequest request, ServerCallContext context)
    {
        (_, int index) = ShortIdUtils.ParseId(request.ShortId);

        Url? url = await urlRepository.GetByUrlId(index, false, context.CancellationToken);
        if (url is null)
        {
            throw RpcExceptionUtils.NotFound(nameof(request.ShortId));
        }

        RedirectReply reply = new()
        {
            Url = new GrpcUrlMapping { ShortId = request.ShortId, DestinationUrl = url.DestinationUrl }
        };

        await IncrementVisitInBackground(request);
        await UpdateUrlMappingCacheInBackground(url);

        return reply;
    }

    public override async Task<Empty> NotifyRedirect(RedirectRequest request, ServerCallContext context)
    {
        await IncrementVisitInBackground(request);
        return new Empty();
    }

    public override async Task<GetUrlByIdReply> GetById(GetUrlByIdRequest request, ServerCallContext context)
    {
        (_, int index) = ShortIdUtils.ParseId(request.ShortId);

        Url? url = await urlRepository.GetByUrlId(index, true, context.CancellationToken);
        if (url is null)
        {
            throw RpcExceptionUtils.NotFound(nameof(request.ShortId));
        }

        UrlStats urlStats = Convert(url);

        GetUrlByIdReply reply = new() { Url = urlStats.Serialize() };

        await UpdateUrlStatsCacheInBackground(urlStats, TimeSpan.FromMinutes(15));

        return reply;
    }

    public override async Task<GetUrlsByIdsReply> GetByIds(GetUrlsByIdsRequest request, ServerCallContext context)
    {
        List<int> ids = [];
        foreach (string shortId in request.ShortIds)
        {
            (_, int index) = ShortIdUtils.ParseId(shortId);
            ids.Add(index);
        }

        IList<Url>? urls = await urlRepository.GetByUrlIds(ids, context.CancellationToken);
        if (urls is null)
        {
            throw RpcExceptionUtils.NotFound(nameof(request.ShortIds));
        }

        GetUrlsByIdsReply reply = new() { Urls = new GetUrlsByIdsReply.Types.Urls() };
        foreach (Url url in urls)
        {
            UrlStats urlStats = Convert(url);
            reply.Urls.Urls_.Add(urlStats.Serialize());

            await UpdateUrlStatsCacheInBackground(urlStats, TimeSpan.FromMinutes(15));
        }

        return reply;
    }

    public override async Task<GetUrlsByUserIdReply> GetByUserId(
        GetUrlsByUserIdRequest request, ServerCallContext context)
    {
        int limit;
        (int, Instant)? cursor = null;
        bool latestFirst;
        if (request.HasFirst)
        {
            latestFirst = true;
            limit = request.First + 1;
            if (request.HasAfter)
            {
                cursor = ShortIdUtils.ParseCursor(request.After);
            }
        }
        else if (request.HasLast)
        {
            latestFirst = false;
            limit = request.Last + 1;
            if (request.HasBefore)
            {
                cursor = ShortIdUtils.ParseCursor(request.Before);
            }
        }
        else
        {
            limit = Constants.MaxUrlsPageSize + 1;
            cursor = null;
            latestFirst = true;
        }

        IList<Url>? urls = await urlRepository.GetByUserId(
            request.UserId,
            limit, cursor, latestFirst,
            context.CancellationToken);
        if (urls is null)
        {
            throw RpcExceptionUtils.NotFound(nameof(urls));
        }

        PageInfo pageInfo = new();
        List<Edge<UrlStats>> edges;
        if (request.HasFirst)
        {
            edges = GetUrlsEdge(urls, request.First);
            if (urls.Count > request.First)
            {
                pageInfo.HasNextPage = true;
                pageInfo.StartCursor = ShortIdUtils.CreateCursor(urls.Last().Id, urls.Last().UpdatedAt);
            }
        }
        else if (request.HasLast)
        {
            edges = GetUrlsEdge(urls, request.Last);
            if (urls.Count > request.Last)
            {
                pageInfo.HasPreviousPage = true;
                pageInfo.EndCursor = ShortIdUtils.CreateCursor(urls.Last().Id, urls.Last().UpdatedAt);
            }
        }
        else
        {
            edges = GetUrlsEdge(urls, Constants.MaxUrlsPageSize);
            if (urls.Count > Constants.MaxUrlsPageSize)
            {
                pageInfo.HasNextPage = true;
                pageInfo.StartCursor = ShortIdUtils.CreateCursor(urls.Last().Id, urls.Last().UpdatedAt);
            }
        }

        Connection<UrlStats> connection = new() { PageInfo = pageInfo, Edges = edges };
        await UpdateUrlStatsPaginationCacheInBackground(
            request.UserId,
            request.HasFirst ? request.First : null, request.HasFirst ? request.After : null,
            request.HasLast ? request.Last : null, request.HasLast ? request.Before : null,
            connection,
            TimeSpan.FromMinutes(15));

        return new GetUrlsByUserIdReply { Connection = connection.Serialize() };
    }

    public override async Task<UpdateUrlReply> Update(UpdateUrlRequest request, ServerCallContext context)
    {
        (_, int id) = ShortIdUtils.ParseId(request.Url.ShortId);

        if (!UrlUtils.IsValidHttpUrl(request.Url.DestinationUrl, out Uri? uri))
        {
            throw RpcExceptionUtils.InvalidArgument(nameof(request.Url.DestinationUrl));
        }

        bool banned = await domainService.IsBanned(uri.Host);
        if (banned)
        {
            throw RpcExceptionUtils.InvalidArgument(nameof(request.Url.DestinationUrl));
        }

        Domain domain = await domainRepository.GetOrAdd(uri.Host, context.CancellationToken);

        bool success = await urlRepository.Update(
            id, request.UserId, domain.Id, request.Url.DestinationUrl,
            context.CancellationToken);

        await RemoveUrlMappingCacheInBackground(request.Url.ShortId);
        await RemoveUrlStatsCacheInBackground(request.Url.ShortId);
        await RemoveUrlStatsPaginationCacheInBackground(request.UserId);

        return new UpdateUrlReply { Success = success };
    }

    public override async Task<DeleteUrlReply> Delete(DeleteUrlRequest request, ServerCallContext context)
    {
        (_, int id) = ShortIdUtils.ParseId(request.ShortId);

        bool success = await urlRepository.Delete(id, request.UserId, context.CancellationToken);

        await RemoveUrlMappingCacheInBackground(request.ShortId);
        await RemoveUrlStatsCacheInBackground(request.ShortId);
        await RemoveUrlStatsPaginationCacheInBackground(request.UserId);

        return new DeleteUrlReply { Success = success };
    }

    private List<Edge<UrlStats>> GetUrlsEdge(IList<Url> urls, int pageSize) =>
        urls
            .Take(pageSize)
            .Select(url => new Edge<UrlStats>
            {
                Cursor = ShortIdUtils.CreateCursor(url.Id, url.UpdatedAt), Node = Convert(url)
            })
            .ToList();

    private async Task ChangeShardTypeInBackground(ShardType type)
    {
        await taskQueue.QueueBackgroundWorkItem(Task);
        return;

        async ValueTask Task(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await shardService.ChangeType(type);
        }
    }

    private async Task UpdateUrlStatsPaginationCacheInBackground(
        int userId,
        int? first, string? after, int? last, string? before,
        Connection<UrlStats> connection, TimeSpan duration)
    {
        string key = SharedCacheUtils.GetUserHashKey(new UserId(shardService.Id, userId));
        string entryKey = SharedCacheUtils.GetPaginationEntryKey(first, after, last, before);
        Func<IServiceScopeFactory, CancellationToken, ValueTask> task =
            CacheUtils.CreateHashSetTask(key, entryKey, connection, duration);

        await taskQueue.QueueBackgroundWorkItem(task);
    }

    private async Task RemoveUrlStatsPaginationCacheInBackground(int userId)
    {
        string key = SharedCacheUtils.GetUserHashKey(new UserId(shardService.Id, userId));
        Func<IServiceScopeFactory, CancellationToken, ValueTask> task = CacheUtils.CreateRemoveTask(key);

        await taskQueue.QueueBackgroundWorkItem(task);
    }

    private async Task UpdateUrlStatsCacheInBackground(UrlStats url, TimeSpan duration)
    {
        string key = SharedCacheUtils.GetUrlStatsKey(ShortIdUtils.ParseId(url.ShortId));
        Func<IServiceScopeFactory, CancellationToken, ValueTask> task = CacheUtils.CreateSetTask(key, url, duration);

        await taskQueue.QueueBackgroundWorkItem(task);
    }

    private async Task RemoveUrlStatsCacheInBackground(string shortId)
    {
        string key = SharedCacheUtils.GetUrlStatsKey(ShortIdUtils.ParseId(shortId));
        Func<IServiceScopeFactory, CancellationToken, ValueTask> task = CacheUtils.CreateRemoveTask(key);

        await taskQueue.QueueBackgroundWorkItem(task);
    }

    private async Task UpdateUrlMappingCacheInBackground(Url url)
    {
        ShortId shortId = new(shardService.Id, url.Id);
        string key = SharedCacheUtils.GetUrlMappingKey(shortId);
        Func<IServiceScopeFactory, CancellationToken, ValueTask> task =
            CacheUtils.CreateSetStringTask(key, url.DestinationUrl, TimeSpan.FromMinutes(5));

        await taskQueue.QueueBackgroundWorkItem(task);
    }

    private async Task RemoveUrlMappingCacheInBackground(string shortId)
    {
        string key = SharedCacheUtils.GetUrlMappingKey(ShortIdUtils.ParseId(shortId));
        Func<IServiceScopeFactory, CancellationToken, ValueTask> task = CacheUtils.CreateRemoveTask(key);

        await taskQueue.QueueBackgroundWorkItem(task);
    }

    private async Task IncrementVisitInBackground(RedirectRequest request)
    {
        await taskQueue.QueueBackgroundWorkItem(Task);
        return;

        async ValueTask Task(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
                IUrlRepository urlRepo = scope.ServiceProvider.GetRequiredService<IUrlRepository>();
                IVisitRepository visitRepository = scope.ServiceProvider.GetRequiredService<IVisitRepository>();
                IHttpUserAgentParserProvider parser =
                    scope.ServiceProvider.GetRequiredService<IHttpUserAgentParserProvider>();

                (_, int id) = ShortIdUtils.ParseId(request.ShortId);

                await urlRepo.IncrementVisit(id, cancellationToken);

                string? country = await geoIpService.GetCountry(IPAddress.Parse(request.Ip));

                UserAgent? userAgent = null;
                if (request.HasUserAgent)
                {
                    userAgent = UserAgent.Parse(parser.Parse(request.UserAgent));
                }

                string? referrer = null;
                if (request.HasReferrer && UrlUtils.IsValidHttpUrl(request.Referrer, out Uri? uri))
                {
                    referrer = uri.Host;
                }

                await visitRepository.Add(id, country, userAgent, referrer, cancellationToken);
            }
        }
    }

    private UrlStats Convert(Url url)
    {
        ViewStats lastDay = new();
        ViewStats lastWeek = new();
        ViewStats lastMonth = new();
        ViewStats allTime = new();

        Instant now = SystemClock.Instance.GetCurrentInstant();
        foreach (Visit visit in url.Visits)
        {
            if (now - visit.CreatedAt <= Duration.FromDays(1))
            {
                AddVisit(lastDay, visit);
            }

            if (now - visit.CreatedAt <= Duration.FromDays(7))
            {
                AddVisit(lastWeek, visit);
            }

            if (now - visit.CreatedAt <= Duration.FromDays(30))
            {
                AddVisit(lastMonth, visit);
            }

            AddVisit(allTime, visit);
        }

        ShortId shortId = new(shardService.Id, url.Id);
        return new UrlStats
        {
            ShortId = shortId.ToString(),
            DestinationUrl = url.DestinationUrl,
            TotalViews = url.TotalViews,
            LastDay = lastDay,
            LastWeek = lastWeek,
            LastMonth = lastMonth,
            AllTime = allTime
        };
    }

    private static void AddVisit(ViewStats stats, Visit visit)
    {
        AddTypes(stats, visit);
        AddPlatforms(stats, visit);
        AddBrowsers(stats, visit);
        AddMobileDeviceTypes(stats, visit);
        AddCountries(stats, visit);
        AddReferrers(stats, visit);

        stats.Views += visit.Total;
    }

    private static void AddTypes(ViewStats stats, Visit visit)
    {
        if (visit.BrowserType > 0)
        {
            stats.AddType(UserAgentType.Browser, visit.BrowserType);
        }

        if (visit.RobotType > 0)
        {
            stats.AddType(UserAgentType.Robot, visit.BrowserType);
        }

        if (visit.UnknownType > 0)
        {
            stats.AddType(UserAgentType.Unknown, visit.BrowserType);
        }
    }

    private static void AddPlatforms(ViewStats stats, Visit visit)
    {
        Dictionary<string, int> platforms = JsonSerializer.Deserialize<Dictionary<string, int>>(visit.Platforms)!;
        AddPlatforms(stats, PlatformType.Windows, visit.Windows, IsWindows, platforms);
        AddPlatforms(stats, PlatformType.Linux, visit.Linux, IsLinux, platforms);
        stats.AddPlatform(PlatformType.Ios, visit.Ios, []);
        AddPlatforms(stats, PlatformType.MacOs, visit.MacOs, IsMacOs, platforms);
        stats.AddPlatform(PlatformType.Android, visit.Android, []);
        AddPlatforms(stats, PlatformType.Other, visit.OtherPlatform, null, platforms);

        return;

        bool IsWindows(string variant)
        {
            List<string> variants = ["Unknown Windows OS", "HP-UX"];
            return variant.StartsWith("Windows ") || variants.Contains(variant);
        }

        bool IsLinux(string variant)
        {
            List<string> variants = ["FreeBSD", "Macintosh", "Linux", "Debian", "GNU/Linux"];
            return variants.Contains(variant);
        }

        bool IsMacOs(string variant)
        {
            List<string> variants = ["Mac OS X", "Power PC Mac"];
            return variants.Contains(variant);
        }
    }

    private static void AddPlatforms(
        ViewStats stats,
        PlatformType type,
        int platformViews,
        Predicate<string>? isPlatform,
        Dictionary<string, int> platforms)
    {
        if (platformViews == 0)
        {
            return;
        }

        Dictionary<string, int> variantsToAdd = [];
        foreach ((string variant, int views) in platforms.ToList())
        {
            if (isPlatform is not null)
            {
                if (!isPlatform(variant))
                {
                    continue;
                }
            }

            variantsToAdd.Add(variant, views);
            platforms.Remove(variant);
        }

        stats.AddPlatform(type, platformViews, variantsToAdd);
    }

    private static void AddBrowsers(ViewStats stats, Visit visit)
    {
        Dictionary<string, int> browsers = JsonSerializer.Deserialize<Dictionary<string, int>>(visit.Browsers)!;
        AddBrowser(stats, BrowserType.Chrome, visit.Chrome, "Chrome", browsers);
        AddBrowser(stats, BrowserType.Edge, visit.Edge, "Edge", browsers);
        AddBrowser(stats, BrowserType.Firefox, visit.Firefox, "Firefox", browsers);
        AddBrowser(stats, BrowserType.InternetExplorer, visit.InternetExplorer, "Internet Explorer", browsers);
        AddBrowser(stats, BrowserType.Opera, visit.Opera, "Opera", browsers);
        AddBrowser(stats, BrowserType.Safari, visit.Safari, "Safari", browsers);
        AddBrowser(stats, BrowserType.Other, visit.OtherBrowser, null, browsers);
    }

    private static void AddBrowser(
        ViewStats stats,
        BrowserType type,
        int browserViews,
        string? browserName,
        Dictionary<string, int> browsers)
    {
        if (browserViews == 0)
        {
            return;
        }

        Dictionary<string, int> versions = [];
        foreach ((string browserVersion, int views) in browsers.ToList())
        {
            int dashIndex = browserVersion.IndexOf('-');
            string browser = browserVersion[..dashIndex];
            string version = browserVersion[(dashIndex + 1)..];

            if (browserName is not null)
            {
                if (browser != browserName)
                {
                    continue;
                }
            }

            versions.Add(version, views);
            browsers.Remove(browserVersion);
        }

        stats.AddBrowser(type, browserViews, versions);
    }

    private static void AddMobileDeviceTypes(ViewStats stats, Visit visit)
    {
        Dictionary<string, int> types = JsonSerializer.Deserialize<Dictionary<string, int>>(visit.MobileDeviceTypes)!;
        foreach ((string type, int views) in types)
        {
            stats.AddMobileDeviceType(type, views);
        }
    }

    private static void AddCountries(ViewStats stats, Visit visit)
    {
        Dictionary<string, int> countries = JsonSerializer.Deserialize<Dictionary<string, int>>(visit.Countries)!;
        foreach ((string country, int views) in countries)
        {
            stats.AddCountry(country, views);
        }
    }

    private static void AddReferrers(ViewStats stats, Visit visit)
    {
        Dictionary<string, int> referrers = JsonSerializer.Deserialize<Dictionary<string, int>>(visit.Referrers)!;
        foreach ((string referrer, int views) in referrers)
        {
            stats.AddReferrer(referrer, views);
        }
    }
}
