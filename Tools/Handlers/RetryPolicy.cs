#nullable enable
using System;
using System.Net;

namespace RahOllamaOnly.Tools.Handlers;

public sealed class RetryPolicy
{
    private readonly Random _jitter = new();

    public int MaxRetries { get; }
    public TimeSpan BaseDelay { get; }

    public RetryPolicy(int maxRetries = 2, TimeSpan? baseDelay = null)
    {
        MaxRetries = Math.Max(0, maxRetries);
        BaseDelay = baseDelay ?? TimeSpan.FromMilliseconds(250);
    }

    public bool ShouldRetry(Exception exception)
    {
        if (exception == null)
            return false;

        if (exception is TimeoutException)
            return true;

        if (exception is ProviderUnavailableException)
            return true;

        if (exception is System.Net.Http.HttpRequestException httpEx)
        {
            if (httpEx.StatusCode == HttpStatusCode.TooManyRequests ||
                httpEx.StatusCode == HttpStatusCode.ServiceUnavailable ||
                httpEx.StatusCode == HttpStatusCode.GatewayTimeout)
                return true;
        }

        return false;
    }

    public TimeSpan GetDelay(int attempt)
    {
        var exponent = Math.Max(0, attempt);
        var delay = TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * Math.Pow(2, exponent));
        var jitter = TimeSpan.FromMilliseconds(_jitter.Next(0, 100));
        return delay + jitter;
    }
}
