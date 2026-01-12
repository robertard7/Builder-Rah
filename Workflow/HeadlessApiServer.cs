#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Common.Json;
using RahBuilder.Common.Text;
using RahBuilder.Headless.Api;
using RahBuilder.Headless.Models;
using RahBuilder.Metrics;
using RahBuilder.Settings;
using RahBuilder.Workflow.Provider;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public sealed class HeadlessApiServer
{
    private readonly AppConfig _cfg;
    private readonly RunTrace _trace;
    private readonly SessionStore _store;
    private readonly string? _token;
    private HttpListener? _listener;

    public HeadlessApiServer(AppConfig cfg, RunTrace trace, SessionStore store)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _token = Environment.GetEnvironmentVariable("BUILDER_RAH_API_TOKEN");
    }

    public void Start(CancellationToken ct)
    {
        var port = _cfg.General.ProviderApiPort;
        var prefix = $"http://localhost:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _trace.Emit($"[api] Listening on {prefix}");
        _ = Task.Run(() => LoopAsync(ct));
    }

    public void Stop()
    {
        try { _listener?.Stop(); } catch { }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(ctx, ct), ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var isError = false;
        try
        {
            if (!Authorize(ctx))
            {
                isError = true;
                await WriteErrorAsync(ctx, 401, ApiError.Unauthorized(), ct).ConfigureAwait(false);
                return;
            }

            var req = ctx.Request;
            var path = req.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            if (path.Length == 0) path = "/";

            if (req.HttpMethod == "GET" && path == "/provider/metrics")
            {
                var metrics = ProviderDiagnosticsHub.LatestMetrics;
                await WriteJsonAsync(ctx, 200, metrics, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "GET" && path == "/metrics/resilience")
            {
                var metrics = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.Snapshot();
                var stateFilter = req.QueryString["state"];
                int? minRetryAttempts = null;
                var minRetryValue = req.QueryString["minRetryAttempts"];
                if (!string.IsNullOrWhiteSpace(minRetryValue))
                {
                    if (!int.TryParse(minRetryValue, out var parsedMinRetry))
                    {
                        isError = true;
                        await WriteErrorAsync(ctx, 400, ApiError.BadRequest("invalid_min_retry_attempts"), ct).ConfigureAwait(false);
                        return;
                    }
                    minRetryAttempts = parsedMinRetry;
                }

                if (minRetryAttempts.HasValue && metrics.RetryAttempts < minRetryAttempts.Value)
                {
                    await WriteEmptyAsync(ctx, 204, ct).ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(stateFilter))
                {
                    if (!TryApplyStateFilter(metrics, stateFilter, out var filtered))
                    {
                        isError = true;
                        await WriteErrorAsync(ctx, 400, ApiError.BadRequest("invalid_state"), ct).ConfigureAwait(false);
                        return;
                    }
                    metrics = filtered;
                }

                await WriteJsonAsync(ctx, 200, metrics, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "GET" && path == "/metrics/resilience/history")
            {
                var minutes = ParseQueryInt(req, "minutes", 60);
                var limit = ParseQueryInt(req, "limit", 300);
                if (minutes <= 0) minutes = 60;
                if (limit <= 0) limit = 300;
                var window = TimeSpan.FromMinutes(minutes);
                var history = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.SnapshotHistory(window, limit);
                await WriteJsonAsync(ctx, 200, history, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "PUT" && path == "/metrics/resilience/reset")
            {
                RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.Reset();
                await WriteJsonAsync(ctx, 200, new { ok = true, resetAt = DateTimeOffset.UtcNow }, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "POST" && path == "/alerts/thresholds")
            {
                var payload = await ReadJsonAsync(req, ct).ConfigureAwait(false);
                var name = payload.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                var openThreshold = payload.TryGetProperty("openThreshold", out var openEl) && openEl.TryGetInt32(out var open)
                    ? open
                    : 0;
                var retryThreshold = payload.TryGetProperty("retryThreshold", out var retryEl) && retryEl.TryGetInt32(out var retry)
                    ? retry
                    : 0;
                var windowMinutes = payload.TryGetProperty("windowMinutes", out var winEl) && winEl.TryGetInt32(out var win)
                    ? win
                    : 60;
                if (openThreshold <= 0 && retryThreshold <= 0)
                {
                    isError = true;
                    await WriteErrorAsync(ctx, 400, ApiError.BadRequest("threshold_required"), ct).ConfigureAwait(false);
                    return;
                }
                var rule = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.AddAlertRule(name, openThreshold, retryThreshold, windowMinutes);
                await WriteJsonAsync(ctx, 201, rule, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "GET" && path == "/alerts")
            {
                var limit = ParseQueryInt(req, "limit", 50);
                if (limit <= 0) limit = 50;
                var rules = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.ListAlertRules();
                var eventsList = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.ListAlertEvents(limit);
                await WriteJsonAsync(ctx, 200, new { rules, events = eventsList }, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "GET" && path == "/healthz")
            {
                await WriteJsonAsync(ctx, 200, new { ok = true, telemetry = TelemetryRegistry.Snapshot() }, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "GET" && path == "/readyz")
            {
                var provider = ProviderDiagnosticsHub.LatestMetrics;
                await WriteJsonAsync(ctx, 200, new { ok = provider.Enabled && provider.Reachable, provider }, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "GET" && path == "/provider/events")
            {
                var limit = ParseQueryInt(req, "limit", 100);
                var offset = ParseQueryInt(req, "offset", 0);
                if (limit <= 0) limit = 100;
                if (offset < 0) offset = 0;

                var eventsList = ProviderDiagnosticsHub.Events
                    .OrderByDescending(e => e.Timestamp)
                    .Skip(offset)
                    .Take(limit)
                    .Select(e => new ProviderEventDto(e.EventType.ToString(), e.Timestamp, e.Message, e.Metadata))
                    .ToList();
                var total = ProviderDiagnosticsHub.Events.Count;
                await WriteJsonAsync(ctx, 200, new { total, limit, offset, items = eventsList }, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "GET" && path == "/sessions")
            {
                var list = _store.ListSessions();
                var resilience = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.Snapshot();
                var resilienceSummary = BuildResilienceSummary(resilience);
                var payload = list.Select(s => new
                {
                    sessionId = s.SessionId,
                    lastTouched = s.LastTouched,
                    resilienceSummary,
                    resilience
                });
                await WriteJsonAsync(ctx, 200, payload, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "POST" && path == "/sessions")
            {
                var state = new SessionState { SessionId = Guid.NewGuid().ToString("N") };
                _store.Save(state);
                var resilience = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.Snapshot();
                var resilienceSummary = BuildResilienceSummary(resilience);
                await WriteJsonAsync(ctx, 201, new
                {
                    sessionId = state.SessionId,
                    lastTouched = state.LastTouched,
                    resilienceSummary,
                    resilience
                }, ct).ConfigureAwait(false);
                return;
            }

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0] == "sessions")
            {
                var sessionId = parts[1];
                if (parts.Length == 2 && req.HttpMethod == "GET")
                {
                    var state = _store.Load(sessionId);
                    if (state == null)
                    {
                        isError = true;
                        await WriteErrorAsync(ctx, 404, ApiError.NotFound(), ct).ConfigureAwait(false);
                        return;
                    }
                    var resilience = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.Snapshot();
                    var resilienceSummary = BuildResilienceSummary(resilience);
                    await WriteJsonAsync(ctx, 200, new
                    {
                        sessionId = state.SessionId,
                        lastTouched = state.LastTouched,
                        resilienceSummary,
                        resilience
                    }, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "messages" && req.HttpMethod == "GET")
                {
                    var state = _store.Load(sessionId);
                    if (state == null)
                    {
                        isError = true;
                        await WriteErrorAsync(ctx, 404, ApiError.NotFound(), ct).ConfigureAwait(false);
                        return;
                    }
                    await WriteJsonAsync(ctx, 200, state.Messages, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "status" && req.HttpMethod == "GET")
                {
                    var state = _store.Load(sessionId);
                    if (state == null)
                    {
                        isError = true;
                        await WriteErrorAsync(ctx, 404, ApiError.NotFound(), ct).ConfigureAwait(false);
                        return;
                    }
                    var workflow = new WorkflowFacade(_cfg, _trace);
                    workflow.ApplySessionState(state);
                    var statusCode = workflow.GetSessionStatusSnapshot();
                    var status = new SessionStatusDto(statusCode.ToString(), StatusMapper.ToDisplay(statusCode));
                    var resilience = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.Snapshot();
                    var resilienceSummary = BuildResilienceSummary(resilience);
                    var resilienceByTool = RahOllamaOnly.Metrics.ResilienceDiagnosticsHub.SnapshotByTool();
                    await WriteJsonAsync(ctx, 200, new { sessionId, status, resilienceSummary, resilience, resilienceByTool }, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "plan" && req.HttpMethod == "GET")
                {
                    var state = _store.Load(sessionId);
                    if (state == null)
                    {
                        isError = true;
                        await WriteErrorAsync(ctx, 404, ApiError.NotFound(), ct).ConfigureAwait(false);
                        return;
                    }
                    var workflow = new WorkflowFacade(_cfg, _trace);
                    workflow.ApplySessionState(state);
                    var plan = workflow.GetPendingPlan();
                    await WriteJsonAsync(ctx, 200, new { sessionId, plan }, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "message" && req.HttpMethod == "POST")
                {
                    var payload = await ReadJsonAsync(req, ct).ConfigureAwait(false);
                    var text = payload.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        isError = true;
                        await WriteErrorAsync(ctx, 400, ApiError.BadRequest("text_required"), ct).ConfigureAwait(false);
                        return;
                    }
                    var response = await RunMessageAsync(sessionId, text, ct).ConfigureAwait(false);
                    if (IsCircuitOpenResponse(response))
                    {
                        isError = true;
                        await WriteCircuitOpenAsync(ctx, ct).ConfigureAwait(false);
                        return;
                    }
                    await WriteJsonAsync(ctx, 200, response, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "attachments" && req.HttpMethod == "POST")
                {
                    var payload = await ReadJsonAsync(req, ct).ConfigureAwait(false);
                    var pathValue = payload.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(pathValue) || !File.Exists(pathValue))
                    {
                        isError = true;
                        await WriteErrorAsync(ctx, 400, ApiError.BadRequest("path_required"), ct).ConfigureAwait(false);
                        return;
                    }

                    var inbox = new AttachmentInbox(_cfg.General, _trace);
                    var result = inbox.AddFiles(new[] { pathValue });
                    var state = _store.Load(sessionId) ?? new SessionState { SessionId = sessionId };
                    state.Attachments = inbox.List().Select(a => new SessionAttachment(
                        a.StoredName, a.OriginalName, a.Kind, a.SizeBytes, a.Sha256, a.Active)).ToList();
                    _store.Save(state);

                    await WriteJsonAsync(ctx, 200, new { ok = result.Ok, message = result.Message, added = result.Added }, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "run" && req.HttpMethod == "POST")
                {
                    var response = await RunPlanAsync(sessionId, ct).ConfigureAwait(false);
                    if (IsCircuitOpenResponse(response))
                    {
                        isError = true;
                        await WriteCircuitOpenAsync(ctx, ct).ConfigureAwait(false);
                        return;
                    }
                    await WriteJsonAsync(ctx, 200, response, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "cancel" && req.HttpMethod == "POST")
                {
                    var state = _store.Load(sessionId);
                    if (state == null)
                    {
                        isError = true;
                        await WriteErrorAsync(ctx, 404, ApiError.NotFound(), ct).ConfigureAwait(false);
                        return;
                    }
                    var workflow = new WorkflowFacade(_cfg, _trace);
                    workflow.ApplySessionState(state);
                    workflow.CancelPlan();
                    var updated = workflow.BuildSessionState();
                    _store.Save(updated);
                    await WriteJsonAsync(ctx, 200, new { sessionId, status = workflow.GetPublicSnapshot() }, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 2 && req.HttpMethod == "DELETE")
                {
                    var state = _store.Load(sessionId);
                    if (state != null)
                    {
                        var inbox = new AttachmentInbox(_cfg.General, _trace);
                        foreach (var attachment in state.Attachments)
                            inbox.Remove(attachment.StoredName);
                    }
                    DeleteArtifactsForSession(sessionId);
                    _store.Delete(sessionId);
                    await WriteJsonAsync(ctx, 200, new { ok = true, sessionId }, ct).ConfigureAwait(false);
                    return;
                }
            }

            isError = true;
            await WriteErrorAsync(ctx, 404, ApiError.NotFound(), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            isError = true;
            await WriteErrorAsync(ctx, 500, ApiError.ServerError(ex.Message), ct).ConfigureAwait(false);
        }
        finally
        {
            sw.Stop();
            TelemetryRegistry.RecordRequest(sw.ElapsedMilliseconds, isError);
        }
    }

    private bool Authorize(HttpListenerContext ctx)
    {
        if (string.IsNullOrWhiteSpace(_token)) return true;
        var header = ctx.Request.Headers["X-Builder-Token"] ?? "";
        return string.Equals(header, _token, StringComparison.Ordinal);
    }

    private async Task<RunResponse> RunMessageAsync(string sessionId, string text, CancellationToken ct)
    {
        var state = _store.Load(sessionId) ?? new SessionState { SessionId = sessionId };
        var workflow = new WorkflowFacade(_cfg, _trace);
        workflow.ApplySessionState(state);
        var responses = new List<string>();
        workflow.UserFacingMessage += msg =>
        {
            if (!string.IsNullOrWhiteSpace(msg))
                responses.Add(UserSafeText.Sanitize(msg));
        };

        await workflow.RouteUserInput(_cfg, text, ct).ConfigureAwait(false);
        var updated = workflow.BuildSessionState();
        _store.Save(updated);
        return new RunResponse(updated.SessionId, responses, workflow.GetPublicSnapshot());
    }

    private async Task<RunResponse> RunPlanAsync(string sessionId, CancellationToken ct)
    {
        var state = _store.Load(sessionId) ?? new SessionState { SessionId = sessionId };
        var workflow = new WorkflowFacade(_cfg, _trace);
        workflow.ApplySessionState(state);
        var responses = new List<string>();
        workflow.UserFacingMessage += msg =>
        {
            if (!string.IsNullOrWhiteSpace(msg))
                responses.Add(UserSafeText.Sanitize(msg));
        };
        await workflow.ApproveAllStepsAsync(ct).ConfigureAwait(false);
        var updated = workflow.BuildSessionState();
        _store.Save(updated);
        return new RunResponse(updated.SessionId, responses, workflow.GetPublicSnapshot());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpListenerRequest req, CancellationToken ct)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
            return JsonDocument.Parse("{}").RootElement;
        return JsonDocument.Parse(body).RootElement;
    }

    private static int ParseQueryInt(HttpListenerRequest req, string key, int fallback)
    {
        var value = req.QueryString[key];
        if (int.TryParse(value, out var parsed))
            return parsed;
        return fallback;
    }

    private static bool TryApplyStateFilter(CircuitMetricsSnapshot metrics, string state, out CircuitMetricsSnapshot filtered)
    {
        switch (state.Trim().ToLowerInvariant())
        {
            case "open":
                filtered = new CircuitMetricsSnapshot(metrics.OpenCount, 0, 0, metrics.RetryAttempts);
                return true;
            case "halfopen":
                filtered = new CircuitMetricsSnapshot(0, metrics.HalfOpenCount, 0, metrics.RetryAttempts);
                return true;
            case "closed":
                filtered = new CircuitMetricsSnapshot(0, 0, metrics.ClosedCount, metrics.RetryAttempts);
                return true;
            default:
                filtered = metrics;
                return false;
        }
    }

    private static string BuildResilienceSummary(CircuitMetricsSnapshot metrics)
    {
        return $"retries: {metrics.RetryAttempts}, open: {metrics.OpenCount}, half-open: {metrics.HalfOpenCount}, closed: {metrics.ClosedCount}";
    }

    private static bool IsCircuitOpenResponse(RunResponse response)
    {
        return response.Responses.Any(msg => msg.Contains("circuit is open", StringComparison.OrdinalIgnoreCase));
    }

    private static Task WriteCircuitOpenAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        ctx.Response.Headers["Retry-After"] = "30";
        var details = new Dictionary<string, object>
        {
            ["retryAfterSeconds"] = 30,
            ["hint"] = "The circuit breaker is open. Retry after backoff."
        };
        return WriteErrorAsync(ctx, 503, ApiError.ServiceUnavailable("circuit_open", details), ct);
    }

    private sealed record RunResponse(string SessionId, List<string> Responses, object Status);

    private void DeleteArtifactsForSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        var repoRoot = _cfg.General.RepoRoot ?? "";
        if (string.IsNullOrWhiteSpace(repoRoot)) return;
        var artifactsRoot = Path.Combine(repoRoot, "Workflow", "ProgramArtifacts");
        if (!Directory.Exists(artifactsRoot)) return;

        var token = "-" + sessionId + "-";
        foreach (var dir in Directory.GetDirectories(artifactsRoot))
        {
            if (dir.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
        foreach (var file in Directory.GetFiles(artifactsRoot, "*.zip"))
        {
            if (file.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    private static async Task WriteJsonAsync(HttpListenerContext ctx, int statusCode, object payload, CancellationToken ct)
    {
        var json = JsonDefaults.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentEncoding = Encoding.UTF8;
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(false);
        ctx.Response.OutputStream.Close();
    }

    private static async Task WriteEmptyAsync(HttpListenerContext ctx, int statusCode, CancellationToken ct)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentLength64 = 0;
        await ctx.Response.OutputStream.FlushAsync(ct).ConfigureAwait(false);
        ctx.Response.OutputStream.Close();
    }

    private static Task WriteErrorAsync(HttpListenerContext ctx, int statusCode, ApiError error, CancellationToken ct)
    {
        return WriteJsonAsync(ctx, statusCode, error, ct);
    }
}
