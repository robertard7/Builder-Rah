#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
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
        try
        {
            if (!Authorize(ctx))
            {
                await WriteJsonAsync(ctx, 401, new { error = "unauthorized" }, ct).ConfigureAwait(false);
                return;
            }

            var req = ctx.Request;
            var path = req.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            if (path.Length == 0) path = "/";

            if (req.HttpMethod == "GET" && path == "/sessions")
            {
                var list = _store.ListSessions();
                var payload = list.Select(s => new { sessionId = s.SessionId, lastTouched = s.LastTouched });
                await WriteJsonAsync(ctx, 200, payload, ct).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod == "POST" && path == "/sessions")
            {
                var state = new SessionState { SessionId = Guid.NewGuid().ToString("N") };
                _store.Save(state);
                await WriteJsonAsync(ctx, 201, new { sessionId = state.SessionId, lastTouched = state.LastTouched }, ct).ConfigureAwait(false);
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
                        await WriteJsonAsync(ctx, 404, new { error = "not_found" }, ct).ConfigureAwait(false);
                        return;
                    }
                    await WriteJsonAsync(ctx, 200, new { sessionId = state.SessionId, lastTouched = state.LastTouched }, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "messages" && req.HttpMethod == "GET")
                {
                    var state = _store.Load(sessionId);
                    if (state == null)
                    {
                        await WriteJsonAsync(ctx, 404, new { error = "not_found" }, ct).ConfigureAwait(false);
                        return;
                    }
                    await WriteJsonAsync(ctx, 200, state.Messages, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "message" && req.HttpMethod == "POST")
                {
                    var payload = await ReadJsonAsync(req, ct).ConfigureAwait(false);
                    var text = payload.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        await WriteJsonAsync(ctx, 400, new { error = "text_required" }, ct).ConfigureAwait(false);
                        return;
                    }
                    var response = await RunMessageAsync(sessionId, text, ct).ConfigureAwait(false);
                    await WriteJsonAsync(ctx, 200, response, ct).ConfigureAwait(false);
                    return;
                }

                if (parts.Length == 3 && parts[2] == "attachments" && req.HttpMethod == "POST")
                {
                    var payload = await ReadJsonAsync(req, ct).ConfigureAwait(false);
                    var pathValue = payload.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(pathValue) || !File.Exists(pathValue))
                    {
                        await WriteJsonAsync(ctx, 400, new { error = "path_required" }, ct).ConfigureAwait(false);
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
                    await WriteJsonAsync(ctx, 200, response, ct).ConfigureAwait(false);
                    return;
                }
            }

            await WriteJsonAsync(ctx, 404, new { error = "not_found" }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(ctx, 500, new { error = ex.Message }, ct).ConfigureAwait(false);
        }
    }

    private bool Authorize(HttpListenerContext ctx)
    {
        if (string.IsNullOrWhiteSpace(_token)) return true;
        var header = ctx.Request.Headers["X-Builder-Token"] ?? "";
        return string.Equals(header, _token, StringComparison.Ordinal);
    }

    private async Task<object> RunMessageAsync(string sessionId, string text, CancellationToken ct)
    {
        var state = _store.Load(sessionId) ?? new SessionState { SessionId = sessionId };
        var workflow = new WorkflowFacade(_cfg, _trace);
        workflow.ApplySessionState(state);
        var responses = new List<string>();
        workflow.UserFacingMessage += msg =>
        {
            if (!string.IsNullOrWhiteSpace(msg))
                responses.Add(msg);
        };

        await workflow.RouteUserInput(_cfg, text, ct).ConfigureAwait(false);
        var updated = workflow.BuildSessionState();
        _store.Save(updated);
        return new { sessionId = updated.SessionId, responses, status = workflow.GetPublicSnapshot() };
    }

    private async Task<object> RunPlanAsync(string sessionId, CancellationToken ct)
    {
        var state = _store.Load(sessionId) ?? new SessionState { SessionId = sessionId };
        var workflow = new WorkflowFacade(_cfg, _trace);
        workflow.ApplySessionState(state);
        var responses = new List<string>();
        workflow.UserFacingMessage += msg =>
        {
            if (!string.IsNullOrWhiteSpace(msg))
                responses.Add(msg);
        };
        await workflow.ApproveAllStepsAsync(ct).ConfigureAwait(false);
        var updated = workflow.BuildSessionState();
        _store.Save(updated);
        return new { sessionId = updated.SessionId, responses, status = workflow.GetPublicSnapshot() };
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpListenerRequest req, CancellationToken ct)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
            return JsonDocument.Parse("{}").RootElement;
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task WriteJsonAsync(HttpListenerContext ctx, int statusCode, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentEncoding = Encoding.UTF8;
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(false);
        ctx.Response.OutputStream.Close();
    }
}
