#nullable enable
using System;
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
            if (path.EndsWith("/api/status", StringComparison.OrdinalIgnoreCase))
            {
                var snapshot = _workflow.GetPublicSnapshot();
                await WriteJsonAsync(ctx, snapshot).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/results", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(ctx, _workflow.GetOutputCards()).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/artifacts", StringComparison.OrdinalIgnoreCase))
            {
                var cards = _workflow.GetOutputCards().Where(c => c.Kind == OutputCardKind.Program).ToList();
                await WriteJsonAsync(ctx, new { cards }).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/api/plan", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(ctx, new
                {
                    plan = _workflow.GetPendingPlan(),
                    session = Guid.NewGuid().ToString("N")
                }).ConfigureAwait(false);
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
                    var session = json.RootElement.TryGetProperty("session", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(session))
                        _workflow.OverrideSession(session);
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
