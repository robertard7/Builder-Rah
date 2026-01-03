#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RahBuilder.Workflow;

public sealed record SessionEvent(string Type, object Data, DateTimeOffset Timestamp);

internal sealed class SessionEventStream : IDisposable
{
    private readonly HttpListenerContext _context;
    private readonly CancellationTokenSource _cts = new();
    private readonly BlockingCollection<SessionEvent> _queue = new();
    private readonly Task _writerTask;

    public string Session { get; }

    public SessionEventStream(string session, HttpListenerContext ctx)
    {
        Session = session;
        _context = ctx;
        _writerTask = Task.Run(() => WriterLoopAsync());
    }

    private async Task WriterLoopAsync()
    {
        try
        {
            _context.Response.StatusCode = 200;
            _context.Response.ContentType = "text/event-stream";
            _context.Response.Headers.Add("Cache-Control", "no-cache");
            _context.Response.SendChunked = true;

            await using var writer = new StreamWriter(_context.Response.OutputStream);
            await WriteEventAsync(writer, new SessionEvent("ping", new { session = Session }, DateTimeOffset.UtcNow)).ConfigureAwait(false);

            foreach (var evt in _queue.GetConsumingEnumerable(_cts.Token))
            {
                await WriteEventAsync(writer, evt).ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            try { _context.Response.OutputStream.Close(); } catch { }
            try { _context.Response.Close(); } catch { }
        }
    }

    private static async Task WriteEventAsync(StreamWriter writer, SessionEvent evt)
    {
        var json = JsonSerializer.Serialize(new { evt.Type, evt.Timestamp, data = evt.Data });
        await writer.WriteAsync($"event: {evt.Type}\n").ConfigureAwait(false);
        await writer.WriteAsync($"data: {json}\n\n").ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    public void Enqueue(SessionEvent evt)
    {
        if (!_queue.IsAddingCompleted)
        {
            _queue.Add(evt);
        }
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

public sealed class SessionEventStreamManager : IDisposable
{
    private readonly ConcurrentDictionary<string, List<SessionEventStream>> _streams = new(StringComparer.OrdinalIgnoreCase);

    public void AddStream(string session, HttpListenerContext ctx)
    {
        var stream = new SessionEventStream(session, ctx);
        var list = _streams.GetOrAdd(session, _ => new List<SessionEventStream>());
        lock (list)
        {
            list.Add(stream);
        }
    }

    public void Publish(string session, string type, object payload)
    {
        var evt = new SessionEvent(type, payload, DateTimeOffset.UtcNow);
        if (_streams.TryGetValue(session, out var list))
        {
            List<SessionEventStream> dead = new();
            lock (list)
            {
                foreach (var s in list)
                {
                    try { s.Enqueue(evt); }
                    catch { dead.Add(s); }
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

    public void Dispose()
    {
        foreach (var list in _streams.Values)
        {
            lock (list)
            {
                foreach (var s in list) s.Dispose();
                list.Clear();
            }
        }
        _streams.Clear();
    }
}
