#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RahBuilder.Workflow;

public sealed class ProviderApiHost : IDisposable
{
    private readonly WorkflowFacade _workflow;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public ProviderApiHost(WorkflowFacade workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    }

    public bool IsRunning => _listener?.IsListening == true;

    public void Start(int port)
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/api/");
        _listener.Start();
        _ = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Close();
        }
        catch { }
        finally
        {
            _cts = null;
            _listener = null;
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        if (_listener == null) return;

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                continue;
            }

            _ = Task.Run(() => HandleAsync(ctx, ct), ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            var session = ctx.Request.QueryString?["session"] ?? "";
            var currentSession = _workflow.GetSessionToken();
            var allowOverride = path.EndsWith("/api/jobs", StringComparison.OrdinalIgnoreCase);

            if (!allowOverride && !string.IsNullOrWhiteSpace(session) && !string.Equals(session, currentSession, StringComparison.Ordinal))
            {
                await WriteJsonAsync(ctx, new { ok = false, error = "session_mismatch" }, HttpStatusCode.Forbidden).ConfigureAwait(false);
                return;
            }

            if (allowOverride && !string.IsNullOrWhiteSpace(session))
                _workflow.OverrideSession(session);

            if (path.EndsWith("/api/status", StringComparison.OrdinalIgnoreCase))
            {
                var snapshot = _workflow.GetPublicSnapshot();
                await WriteJsonAsync(ctx, snapshot).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/session", StringComparison.OrdinalIgnoreCase) && ctx.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                var id = SessionManager.Instance.CreateSession();
                _workflow.OverrideSession(id);
                await WriteJsonAsync(ctx, new { ok = true, session = id }).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/results", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(ctx, _workflow.GetOutputCards()).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/sessions", StringComparison.OrdinalIgnoreCase))
            {
                var sessions = SessionManager.Instance.ListSessions();
                await WriteJsonAsync(ctx, new { ok = true, sessions }).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/output", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(ctx, _workflow.GetOutputCards()).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/artifacts", StringComparison.OrdinalIgnoreCase))
            {
                var artifacts = _workflow.GetArtifacts();
                var packages = _workflow.GetArtifactPackages();
                await WriteJsonAsync(ctx, new { ok = true, artifacts, packages, session = currentSession }).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/history", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(ctx, _workflow.GetHistorySnapshot()).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/logs", StringComparison.OrdinalIgnoreCase))
            {
                var snap = SessionManager.Instance.Snapshot(currentSession);
                await WriteJsonAsync(ctx, new { ok = true, logs = snap.Logs, session = currentSession }).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/session/reset", StringComparison.OrdinalIgnoreCase) && ctx.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                var ok = SessionManager.Instance.Reset(currentSession);
                await WriteJsonAsync(ctx, new { ok, session = currentSession }).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/stream", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.Headers.Add("Cache-Control", "no-cache");
                await using var writer = new StreamWriter(ctx.Response.OutputStream);
                await writer.WriteAsync("event: ping\n");
                await writer.WriteAsync($"data: {{\"session\":\"{currentSession}\"}}\n\n");
                await writer.FlushAsync().ConfigureAwait(false);
                ctx.Response.Close();
                return;
            }

            if (path.EndsWith("/api/artifacts/download", StringComparison.OrdinalIgnoreCase))
            {
                var artifacts = _workflow.GetArtifacts();
                var hash = ctx.Request.QueryString?["hash"] ?? "";
                var selected = string.IsNullOrWhiteSpace(hash)
                    ? artifacts.LastOrDefault()
                    : artifacts.FirstOrDefault(a => string.Equals(a.Hash, hash, StringComparison.OrdinalIgnoreCase));
                var zip = selected?.ZipPath ?? _workflow.GetArtifactPackages().LastOrDefault();
                if (string.IsNullOrWhiteSpace(zip) || !System.IO.File.Exists(zip))
                {
                    await WriteJsonAsync(ctx, new { ok = false, error = "not_found" }, HttpStatusCode.NotFound).ConfigureAwait(false);
                    return;
                }

                var bytes = await System.IO.File.ReadAllBytesAsync(zip, ct).ConfigureAwait(false);
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                ctx.Response.ContentType = "application/zip";
                ctx.Response.ContentLength64 = bytes.LongLength;
                ctx.Response.AddHeader("Content-Disposition", $"attachment; filename=\"{System.IO.Path.GetFileName(zip)}\"");
                await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                ctx.Response.Close();
                return;
            }

            if (path.EndsWith("/api/plan", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(ctx, _workflow.GetPlanSnapshot()).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/jobs", StringComparison.OrdinalIgnoreCase) && ctx.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new System.IO.StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                try
                {
                    var json = JsonDocument.Parse(body);
                    var text = json.RootElement.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                    var sessionBody = json.RootElement.TryGetProperty("session", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(sessionBody))
                        _workflow.OverrideSession(sessionBody);
                    _ = _workflow.RouteUserInput(text, CancellationToken.None);
                    await WriteJsonAsync(ctx, new { ok = true }).ConfigureAwait(false);
                }
                catch
                {
                    await WriteJsonAsync(ctx, new { ok = false, error = "invalid_payload" }, HttpStatusCode.BadRequest).ConfigureAwait(false);
                }
                return;
            }

            await WriteJsonAsync(ctx, new { ok = false, error = "not_found" }, HttpStatusCode.NotFound).ConfigureAwait(false);
        }
        catch
        {
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
        }
    }

    private static async Task WriteJsonAsync(HttpListenerContext ctx, object payload, HttpStatusCode code = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = (int)code;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentEncoding = Encoding.UTF8;
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        ctx.Response.Close();
    }

    public void Dispose() => Stop();
}
