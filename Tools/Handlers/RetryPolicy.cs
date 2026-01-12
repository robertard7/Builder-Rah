#nullable enable
using System;
using System.Net;

namespace RahOllamaOnly.Tools.Handlers;

public sealed class RetryPolicy
{
    private readonly Random _jitter = new();
    private readonly RetryPolicyConfig _config;

    public int MaxRetries => _config.MaxRetries;
    public TimeSpan BaseDelay => _config.BaseDelay;
    public TimeSpan MaxDelay => _config.MaxDelay;
    public TimeSpan JitterRange => _config.JitterRange;

    public RetryPolicy(RetryPolicyConfig? config = null)
    {
        _config = config ?? RetryPolicyConfig.Default;
    }

    public RetryPolicy(int maxRetries, TimeSpan? baseDelay = null, TimeSpan? maxDelay = null, TimeSpan? jitterRange = null)
        : this(new RetryPolicyConfig(maxRetries, baseDelay, maxDelay, jitterRange))
    {
    }

    public bool ShouldRetry(Exception exception)
    {
        if (exception == null)
            return false;

        if (_config.RetryOnTimeout && exception is TimeoutException)
            return true;

        if (_config.RetryOnProviderUnavailable && exception is ProviderUnavailableException)
            return true;

        if (exception is System.Net.Http.HttpRequestException httpEx)
        {
            if (httpEx.StatusCode.HasValue && _config.RetriableStatusCodes.Contains(httpEx.StatusCode.Value))
                return true;
        }

        return false;
    }

    public TimeSpan GetDelay(int attempt)
    {
        var exponent = Math.Max(0, attempt);
        var delayMs = BaseDelay.TotalMilliseconds * Math.Pow(2, exponent);
        var delay = TimeSpan.FromMilliseconds(delayMs);
        if (delay > MaxDelay)
            delay = MaxDelay;

        var jitterMs = JitterRange.TotalMilliseconds;
        if (jitterMs > 0)
            delay += TimeSpan.FromMilliseconds(_jitter.NextDouble() * jitterMs);

        return delay;
    }
}
