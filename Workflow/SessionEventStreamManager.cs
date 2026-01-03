#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RahBuilder.Workflow;

internal sealed class EventStream : IDisposable
{
    private readonly HttpListenerContext _ctx;
    private readonly CancellationTokenSource _cts = new();
    private readonly BlockingCollection<SessionEvent> _queue = new();

    public string Session { get; }

    public EventStream(string session, HttpListenerContext ctx, IEnumerable<SessionEvent>? initial = null)
    {
        Session = session;
        _ctx = ctx;
        if (initial != null)
        {
            foreach (var ev in initial)
                _queue.Add(ev);
        }
        _ = Task.Run(WriterLoopAsync);
    }

    public void Close() => Dispose();

    public void Enqueue(SessionEvent evt)
    {
        if (!_queue.IsAddingCompleted)
        {
            _queue.Add(evt);
        }
    }

    private async Task WriterLoopAsync()
    {
        var resp = _ctx.Response;
        resp.AddHeader("Cache-Control", "no-cache");
        resp.ContentType = "text/event-stream";
        resp.AddHeader("Connection", "keep-alive");

        await resp.OutputStream.FlushAsync().ConfigureAwait(false);
        await using var writer = new StreamWriter(resp.OutputStream);
        await WriteEventAsync(writer, new SessionEvent("ping", new { }, DateTimeOffset.UtcNow)).ConfigureAwait(false);

        try
        {
            foreach (var ev in _queue.GetConsumingEnumerable(_cts.Token))
            {
                await WriteEventAsync(writer, ev).ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            try { resp.OutputStream.Close(); } catch { }
            try { resp.Close(); } catch { }
        }
    }

    private static async Task WriteEventAsync(StreamWriter writer, SessionEvent ev)
    {
        var json = JsonSerializer.Serialize(ev);
        await writer.WriteAsync($"event: {ev.Type}\n").ConfigureAwait(false);
        await writer.WriteAsync($"data: {json}\n\n").ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
            _queue.CompleteAdding();
        }
        catch { }
    }
}

internal sealed class SessionEventStreamManager
{
    private readonly ConcurrentDictionary<string, List<EventStream>> _streams = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string session, HttpListenerContext ctx)
    {
        var history = SessionManager.TryGet(session, out var rec) ? rec.History : new List<SessionEvent>();
        var evs = new EventStream(session, ctx, history);
        var list = _streams.GetOrAdd(session, _ => new());
        lock (list) list.Add(evs);
    }

    public SessionEventStreamManager()
    {
        SessionEvents.EventAdded += OnEvent;
    }

    private void OnEvent(string session, SessionEvent evt)
    {
        if (!_streams.TryGetValue(session, out var list)) return;

        List<EventStream> dead = new();
        lock (list)
        {
            foreach (var stream in list)
            {
                try { stream.Enqueue(evt); }
                catch { dead.Add(stream); }
            }

            if (dead.Count > 0)
            {
                foreach (var d in dead)
                {
                    list.Remove(d);
                    d.Dispose();
                }
            }
        }
    }
}
