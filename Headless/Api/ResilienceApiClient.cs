#nullable enable
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RahBuilder.Headless.Api;

public sealed class ResilienceApiClient
{
    private readonly HttpClient _http;

    public ResilienceApiClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<CircuitMetricsSnapshot?> GetMetricsAsync(string? state = null, int? minRetryAttempts = null, CancellationToken ct = default)
    {
        var query = QueryBuilder.Create()
            .Add("state", state)
            .Add("minRetryAttempts", minRetryAttempts?.ToString());
        return await _http.GetFromJsonAsync<CircuitMetricsSnapshot>($"/metrics/resilience{query}", ct).ConfigureAwait(false);
    }

    public async Task<ResilienceMetricsSample[]?> GetHistoryAsync(int minutes = 60, int limit = 300, CancellationToken ct = default)
    {
        var query = QueryBuilder.Create()
            .Add("minutes", minutes.ToString())
            .Add("limit", limit.ToString());
        return await _http.GetFromJsonAsync<ResilienceMetricsSample[]>($"/metrics/resilience/history{query}", ct).ConfigureAwait(false);
    }

    public async Task<ApiOkResponse?> ResetMetricsAsync(CancellationToken ct = default)
    {
        using var response = await _http.PutAsync("/metrics/resilience/reset", content: null, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ApiOkResponse>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<ResilienceAlertRule?> CreateAlertRuleAsync(ResilienceAlertRuleRequest request, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("/alerts/thresholds", request, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResilienceAlertRule>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<ResilienceAlertsResponse?> GetAlertsAsync(int limit = 50, CancellationToken ct = default)
    {
        var query = QueryBuilder.Create()
            .Add("limit", limit.ToString());
        return await _http.GetFromJsonAsync<ResilienceAlertsResponse>($"/alerts{query}", ct).ConfigureAwait(false);
    }

    public sealed record ApiOkResponse(bool Ok, DateTimeOffset ResetAt);

    public sealed record ResilienceMetricsSample(DateTimeOffset Timestamp, CircuitMetricsSnapshot Metrics);

    public sealed record CircuitMetricsSnapshot(int OpenCount, int HalfOpenCount, int ClosedCount, int RetryAttempts);

    public sealed record ResilienceAlertRuleRequest(string? Name, int OpenThreshold, int RetryThreshold, int WindowMinutes);

    public sealed record ResilienceAlertRule(string Id, string Name, int OpenThreshold, int RetryThreshold, int WindowMinutes, bool Enabled);

    public sealed record ResilienceAlertEvent(string Id, string RuleId, string Message, DateTimeOffset TriggeredAt, int OpenDelta, int RetryDelta);

    public sealed record ResilienceAlertsResponse(ResilienceAlertRule[] Rules, ResilienceAlertEvent[] Events);

    private sealed class QueryBuilder
    {
        private readonly System.Collections.Generic.List<string> _parts = new();

        public static QueryBuilder Create() => new();

        public QueryBuilder Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                _parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            return this;
        }

        public override string ToString() => _parts.Count == 0 ? "" : "?" + string.Join("&", _parts);
    }
}
