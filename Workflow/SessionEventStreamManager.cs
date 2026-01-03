#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RahBuilder.Workflow;

internal class EventStream
{
    private readonly HttpListenerContext _ctx;
    private readonly CancellationTokenSource _cts = new();

    public EventStream(HttpListenerContext ctx) => _ctx = ctx;

    public void Close() => _cts.Cancel();

    public async Task Run(string sessionId)
    {
        var resp = _ctx.Response;
        resp.AddHeader("Cache-Control", "no-cache");
        resp.ContentType = "text/event-stream";
        resp.AddHeader("Connection", "keep-alive");

        await resp.OutputStream.FlushAsync().ConfigureAwait(false);

        var lastIndex = 0;
        if (SessionManager.TryGet(sessionId, out var session))
        {
            foreach (var ev in session.History)
            {
                await WriteEventAsync(resp, ev).ConfigureAwait(false);
            }
            lastIndex = session.History.Count;
        }

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(500, _cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (_cts.IsCancellationRequested)
                break;

            if (SessionManager.TryGet(sessionId, out session) && session.History.Count > lastIndex)
            {
                for (var i = lastIndex; i < session.History.Count; i++)
                {
                    await WriteEventAsync(resp, session.History[i]).ConfigureAwait(false);
                }
                lastIndex = session.History.Count;
            }
        }
    }

    private static async Task WriteEventAsync(HttpListenerResponse resp, SessionEvent ev)
    {
        var json = JsonSerializer.Serialize(ev);
        var bytes = Encoding.UTF8.GetBytes($"event: {ev.Type}\ndata: {json}\n\n");
        await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        await resp.OutputStream.FlushAsync().ConfigureAwait(false);
    }
}

internal class SessionEventStreamManager
{
    private readonly ConcurrentDictionary<string, List<EventStream>> _streams = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string session, HttpListenerContext ctx)
    {
        var evs = new EventStream(ctx);
        var list = _streams.GetOrAdd(session, _ => new());
        lock (list) list.Add(evs);
        _ = evs.Run(session);
    }
}
