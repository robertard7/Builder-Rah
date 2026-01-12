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

    public async Task<ResilienceMetricsResponse?> GetMetricsAsync(string? state = null, int? minRetryAttempts = null, CancellationToken ct = default)
    {
        var query = QueryBuilder.Create()
            .Add("state", state)
            .Add("minRetryAttempts", minRetryAttempts?.ToString());
        return await _http.GetFromJsonAsync<ResilienceMetricsResponse>($"/metrics/resilience{query}", ct).ConfigureAwait(false);
    }

    public async Task<ResilienceHistoryResponse?> GetHistoryAsync(int minutes = 60, int limit = 300, int page = 1, int perPage = 300, CancellationToken ct = default)
    {
        var query = QueryBuilder.Create()
            .Add("minutes", minutes.ToString())
            .Add("limit", limit.ToString())
            .Add("page", page.ToString())
            .Add("perPage", perPage.ToString());
        return await _http.GetFromJsonAsync<ResilienceHistoryResponse>($"/metrics/resilience/history{query}", ct).ConfigureAwait(false);
    }

    public async Task<ResilienceHistoryResponse?> GetHistoryRangeAsync(DateTimeOffset? start, DateTimeOffset? end, int? limit = null, int? page = null, int? perPage = null, int? bucketMinutes = null, CancellationToken ct = default)
    {
        var query = QueryBuilder.Create()
            .Add("start", start?.ToString("O"))
            .Add("end", end?.ToString("O"))
            .Add("limit", limit?.ToString())
            .Add("page", page?.ToString())
            .Add("perPage", perPage?.ToString())
            .Add("bucketMinutes", bucketMinutes?.ToString());
        return await _http.GetFromJsonAsync<ResilienceHistoryResponse>($"/metrics/resilience/history{query}", ct).ConfigureAwait(false);
    }

    public async Task<ResilienceResetResponse?> ResetMetricsAsync(CancellationToken ct = default)
    {
        using var response = await _http.PutAsync("/metrics/resilience/reset", content: null, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResilienceResetResponse>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<ResilienceResetResponse?> ResetMetricsPostAsync(CancellationToken ct = default)
    {
        using var response = await _http.PostAsync("/metrics/resilience/reset", content: null, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResilienceResetResponse>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<ResilienceAlertRuleResponse?> CreateAlertRuleAsync(ResilienceAlertRuleRequest request, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("/alerts/thresholds", request, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResilienceAlertRuleResponse>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<ResilienceAlertsResponse?> GetAlertsAsync(int limit = 50, string? severity = null, bool includeAcknowledged = true, CancellationToken ct = default)
    {
        var query = QueryBuilder.Create()
            .Add("limit", limit.ToString())
            .Add("severity", severity)
            .Add("includeAcknowledged", includeAcknowledged.ToString());
        return await _http.GetFromJsonAsync<ResilienceAlertsResponse>($"/alerts{query}", ct).ConfigureAwait(false);
    }

    public async Task<ResilienceDeleteResponse?> DeleteAlertsAsync(string? ruleId = null, CancellationToken ct = default)
    {
        var query = QueryBuilder.Create()
            .Add("ruleId", ruleId);
        using var response = await _http.DeleteAsync($"/alerts{query}", ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResilienceDeleteResponse>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<ResilienceAlertRuleResponse?> CreateAlertAsync(ResilienceAlertRuleRequest request, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("/alerts", request, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResilienceAlertRuleResponse>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<ResilienceAlertRuleResponse?> UpdateAlertRuleAsync(string ruleId, ResilienceAlertRuleUpdate request, CancellationToken ct = default)
    {
        using var response = await _http.PatchAsJsonAsync($"/alerts/{ruleId}", request, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResilienceAlertRuleResponse>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<ResilienceAlertEventResponse?> AcknowledgeAlertAsync(string eventId, CancellationToken ct = default)
    {
        using var response = await _http.PatchAsJsonAsync($"/alerts/events/{eventId}", new { acknowledged = true }, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResilienceAlertEventResponse>(cancellationToken: ct).ConfigureAwait(false);
    }

    public sealed record ResilienceMetadata(string Version, DateTimeOffset Timestamp, string RequestId);

    public sealed record ResilienceMetricsResponse(ResilienceMetadata Metadata, CircuitMetricsSnapshot Data);

    public sealed record ResilienceMetricsSample(DateTimeOffset Timestamp, CircuitMetricsSnapshot Metrics);

    public sealed record ResilienceHistoryResponse(ResilienceMetadata Metadata, int Total, int Page, int PerPage, ResilienceMetricsSample[] Items);

    public sealed record CircuitMetricsSnapshot(int OpenCount, int HalfOpenCount, int ClosedCount, int RetryAttempts);

    public sealed record ResilienceResetResponse(ResilienceMetadata Metadata, bool Ok, DateTimeOffset ResetAt);

    public sealed record ResilienceAlertRuleRequest(string? Name, int OpenThreshold, int RetryThreshold, int WindowMinutes, string? Severity);

    public sealed record ResilienceAlertRuleUpdate(
        string? Name,
        int? OpenThreshold,
        int? RetryThreshold,
        int? WindowMinutes,
        string? Severity,
        bool? Enabled);

    public sealed record ResilienceAlertRule(string Id, string Name, int OpenThreshold, int RetryThreshold, int WindowMinutes, string Severity, bool Enabled);

    public sealed record ResilienceAlertRuleResponse(ResilienceMetadata Metadata, ResilienceAlertRule Data);

    public sealed record ResilienceAlertRuleSummary(
        string Id,
        string Name,
        int OpenThreshold,
        int RetryThreshold,
        int WindowMinutes,
        string Severity,
        bool Enabled,
        ResilienceAlertEvent[] RecentEvents);

    public sealed record ResilienceAlertEvent(
        string Id,
        string RuleId,
        string Message,
        string Severity,
        DateTimeOffset TriggeredAt,
        int OpenDelta,
        int RetryDelta,
        bool Acknowledged,
        DateTimeOffset? AcknowledgedAt);

    public sealed record ResilienceAlertEventResponse(ResilienceMetadata Metadata, ResilienceAlertEvent Data);

    public sealed record ResilienceAlertsResponse(ResilienceMetadata Metadata, ResilienceAlertRuleSummary[] Rules, ResilienceAlertEvent[] Events);

    public sealed record ResilienceDeleteResponse(ResilienceMetadata Metadata, bool Ok, string? RuleId);

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
