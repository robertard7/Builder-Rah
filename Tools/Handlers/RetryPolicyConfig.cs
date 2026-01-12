#nullable enable
using System;
using System.Collections.Generic;
using System.Net;

namespace RahOllamaOnly.Tools.Handlers;

public sealed record RetryPolicyConfig(
    int MaxRetries,
    TimeSpan BaseDelay,
    TimeSpan MaxDelay,
    TimeSpan JitterRange,
    IReadOnlyCollection<HttpStatusCode> RetriableStatusCodes,
    bool RetryOnTimeout,
    bool RetryOnProviderUnavailable)
{
    public static RetryPolicyConfig Default => new(
        MaxRetries: 2,
        BaseDelay: TimeSpan.FromMilliseconds(250),
        MaxDelay: TimeSpan.FromSeconds(5),
        JitterRange: TimeSpan.FromMilliseconds(100),
        RetriableStatusCodes: new[]
        {
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout
        },
        RetryOnTimeout: true,
        RetryOnProviderUnavailable: true);

    public RetryPolicyConfig(
        int maxRetries = 2,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        TimeSpan? jitterRange = null,
        IReadOnlyCollection<HttpStatusCode>? retriableStatusCodes = null,
        bool retryOnTimeout = true,
        bool retryOnProviderUnavailable = true)
        : this(
            Math.Max(0, maxRetries),
            baseDelay ?? TimeSpan.FromMilliseconds(250),
            maxDelay ?? TimeSpan.FromSeconds(5),
            jitterRange ?? TimeSpan.FromMilliseconds(100),
            retriableStatusCodes ?? new[]
            {
                HttpStatusCode.TooManyRequests,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.GatewayTimeout
            },
            retryOnTimeout,
            retryOnProviderUnavailable)
    {
    }
}
