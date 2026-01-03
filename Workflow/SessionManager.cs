#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RahBuilder.Workflow;

public sealed record SessionSnapshot(
    string Session,
    DateTimeOffset Started,
    DateTimeOffset LastActive,
    IReadOnlyList<OutputCard> Cards,
    object Memory,
    IReadOnlyList<string> Artifacts,
    IReadOnlyList<string> Logs);

public sealed class SessionManager
{
    private static readonly Lazy<SessionManager> _lazy = new(() => new SessionManager());
    public static SessionManager Instance => _lazy.Value;

    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _storageRoot;

    private SessionManager()
    {
        _storageRoot = Path.Combine(AppContext.BaseDirectory, "Workflow", "Sessions");
        Directory.CreateDirectory(_storageRoot);
        LoadExisting();
    }

    public string CreateSession()
    {
        var id = Guid.NewGuid().ToString("N");
        var rec = new SessionRecord(id, DateTimeOffset.UtcNow);
        _sessions[id] = rec;
        Persist(rec);
        return id;
    }

    public SessionRecord GetOrCreate(string session)
    {
        if (string.IsNullOrWhiteSpace(session))
            return _sessions[CreateSession()];

        return _sessions.GetOrAdd(session, s =>
        {
            var rec = new SessionRecord(s, DateTimeOffset.UtcNow);
            Persist(rec);
            return rec;
        });
    }

    public void AppendCard(string session, OutputCard card)
    {
        if (!_sessions.TryGetValue(session, out var rec))
            rec = GetOrCreate(session);

        rec.Cards.Add(OutputCard.Truncate(card));
        rec.LastActive = DateTimeOffset.UtcNow;
        Persist(rec);
    }

    public void AppendLog(string session, string log)
    {
        if (string.IsNullOrWhiteSpace(log)) return;
        if (!_sessions.TryGetValue(session, out var rec))
            rec = GetOrCreate(session);

        rec.Logs.Add("[" + DateTimeOffset.UtcNow.ToString("O") + "] " + log.Trim());
        rec.LastActive = DateTimeOffset.UtcNow;
        Persist(rec);
    }

    public void UpdateMemory(string session, object memory)
    {
        if (!_sessions.TryGetValue(session, out var rec))
            rec = GetOrCreate(session);

        rec.Memory = memory;
        rec.LastActive = DateTimeOffset.UtcNow;
        Persist(rec);
    }

    public void UpdateArtifacts(string session, IReadOnlyList<string> artifacts)
    {
        if (!_sessions.TryGetValue(session, out var rec))
            rec = GetOrCreate(session);

        rec.Artifacts = artifacts?.ToList() ?? new List<string>();
        rec.LastActive = DateTimeOffset.UtcNow;
        Persist(rec);
    }

    public SessionSnapshot Snapshot(string session)
    {
        var rec = GetOrCreate(session);
        return new SessionSnapshot(rec.Session, rec.StartedUtc, rec.LastActive, rec.Cards.ToList(), rec.Memory ?? new { }, rec.Artifacts.ToList(), rec.Logs.ToList());
    }

    public bool Reset(string session)
    {
        if (!_sessions.TryGetValue(session, out var rec))
            return false;

        var fresh = new SessionRecord(session, DateTimeOffset.UtcNow);
        _sessions[session] = fresh;
        Persist(fresh);
        return true;
    }

    private void LoadExisting()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_storageRoot, "session-*.json"))
            {
                var json = File.ReadAllText(file);
                var rec = JsonSerializer.Deserialize<SessionRecordDto>(json);
                if (rec == null || string.IsNullOrWhiteSpace(rec.Session))
                    continue;
                var record = new SessionRecord(rec.Session, rec.StartedUtc)
                {
                    LastActive = rec.LastActive,
                    Cards = rec.Cards?.ToList() ?? new List<OutputCard>(),
                    Artifacts = rec.Artifacts?.ToList() ?? new List<string>(),
                    Logs = rec.Logs?.ToList() ?? new List<string>(),
                    Memory = rec.Memory
                };
                _sessions[rec.Session] = record;
            }
        }
        catch { }
    }

    private void Persist(SessionRecord rec)
    {
        try
        {
            var dto = new SessionRecordDto
            {
                Session = rec.Session,
                StartedUtc = rec.StartedUtc,
                LastActive = rec.LastActive,
                Cards = rec.Cards,
                Artifacts = rec.Artifacts,
                Logs = rec.Logs,
                Memory = rec.Memory
            };
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(_storageRoot, "session-" + rec.Session + ".json"), json);
        }
        catch { }
    }
}

public sealed class SessionRecord
{
    public string Session { get; }
    public DateTimeOffset StartedUtc { get; }
    public DateTimeOffset LastActive { get; set; }
    public List<OutputCard> Cards { get; set; } = new();
    public List<string> Artifacts { get; set; } = new();
    public List<string> Logs { get; set; } = new();
    public object Memory { get; set; } = new();

    public SessionRecord(string session, DateTimeOffset started)
    {
        Session = session;
        StartedUtc = started;
        LastActive = started;
    }
}

internal sealed class SessionRecordDto
{
    public string Session { get; set; } = "";
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset LastActive { get; set; }
    public List<OutputCard>? Cards { get; set; }
    public List<string>? Artifacts { get; set; }
    public List<string>? Logs { get; set; }
    public object? Memory { get; set; }
}
