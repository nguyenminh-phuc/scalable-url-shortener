using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;

namespace Shortener.Shared.Services;

public interface IQueries
{
    IDictionary<string, string?> ToQueries();
}

public interface IHttpService
{
    Task<TResponse?> Get<TResponse>(string uri, int retries = 1, CancellationToken cancellationToken = default);

    Task<TResponse?> Get<TResponse>(string uri, IQueries queries, int retries = 1,
        CancellationToken cancellationToken = default);

    Task<TResponse?> Post<TRequest, TResponse>(string uri, TRequest request, int retries = 1,
        CancellationToken cancellationToken = default);
}

public sealed class HttpService(ILogger<HttpService> logger, HttpClient client) : IHttpService
{
    public async Task<TResponse?> Get<TResponse>(string uri, int retries, CancellationToken cancellationToken)
    {
        AsyncRetryPolicy<HttpResponseMessage> retryPolicy = GetRetryPolicy(retries);

        HttpResponseMessage? response = await retryPolicy.ExecuteAsync(() => client.GetAsync(uri, cancellationToken));
        if (response is not { IsSuccessStatusCode: true })
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    public async Task<TResponse?> Get<TResponse>(string uri, IQueries queries, int retries,
        CancellationToken cancellationToken)
    {
        AsyncRetryPolicy<HttpResponseMessage> retryPolicy = GetRetryPolicy(retries);
        string fullUri = QueryHelpers.AddQueryString(uri, queries.ToQueries());

        HttpResponseMessage? response =
            await retryPolicy.ExecuteAsync(() => client.GetAsync(fullUri, cancellationToken));
        if (response is not { IsSuccessStatusCode: true })
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    public async Task<TResponse?> Post<TRequest, TResponse>(string uri, TRequest request, int retries,
        CancellationToken cancellationToken)
    {
        AsyncRetryPolicy<HttpResponseMessage> retryPolicy = GetRetryPolicy(retries);

        HttpResponseMessage? response =
            await retryPolicy.ExecuteAsync(() => client.PostAsJsonAsync(uri, request, cancellationToken));
        if (response is not { IsSuccessStatusCode: true })
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    private AsyncRetryPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount)
    {
        AsyncRetryPolicy<HttpResponseMessage>? retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (outcome, timespan, retryAttempt, _) =>
                {
                    string? error = outcome.Exception?.Message;
                    error ??= outcome.Result.ReasonPhrase;

                    logger.LogDebug("Retry {RetryAttempt} after {Seconds} due to: {Error}",
                        retryAttempt,
                        timespan.Seconds,
                        error);
                }
            );

        return retryPolicy;
    }
}
