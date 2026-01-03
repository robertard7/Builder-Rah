#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RahBuilder.Workflow;

public sealed record SessionHistoryEvent(string Type, DateTimeOffset Timestamp, object Data);

public sealed record PlanVersionStep(string Id, string Title, string? ToolId, string? Why, IReadOnlyDictionary<string, string>? Inputs);

public sealed record PlanVersionSnapshot(int Version, DateTimeOffset CreatedUtc, IReadOnlyList<PlanVersionStep> Steps, string? PlanJson);

public sealed record SessionSnapshot(
    string Session,
    DateTimeOffset Started,
    DateTimeOffset LastActive,
    IReadOnlyList<OutputCard> Cards,
    object Memory,
    IReadOnlyList<string> Artifacts,
    IReadOnlyList<string> Logs,
    IReadOnlyList<SessionHistoryEvent> History,
    IReadOnlyList<PlanVersionSnapshot> Plans);

public sealed class SessionManager
{
    private static readonly Lazy<SessionManager> _lazy = new(() => new SessionManager());
    public static SessionManager Instance => _lazy.Value;

    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _expiry = TimeSpan.FromHours(12);
    private readonly string _storageRoot;

    private SessionManager()
    {
        _storageRoot = Path.Combine(AppContext.BaseDirectory, "Workflow", "Sessions");
        Directory.CreateDirectory(_storageRoot);
        LoadExisting();
        PurgeExpired();
    }

    public string CreateSession()
    {
        var id = Guid.NewGuid().ToString("N");
        var rec = new SessionRecord(id, DateTimeOffset.UtcNow);
        _sessions[id] = rec;
        Persist(rec);
        return id;
    }

    public IReadOnlyList<SessionSnapshot> ListSessions()
    {
        PurgeExpired();
        return _sessions.Values.Select(r => Snapshot(r.Session)).ToList();
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

    public bool TryGetSnapshot(string session, out SessionSnapshot snapshot)
    {
        snapshot = default!;
        if (string.IsNullOrWhiteSpace(session) || !_sessions.TryGetValue(session, out _))
            return false;

        snapshot = Snapshot(session);
        return true;
    }

    public bool Exists(string session) => !string.IsNullOrWhiteSpace(session) && _sessions.ContainsKey(session);

    public void AppendCard(string session, OutputCard card)
    {
        if (!_sessions.TryGetValue(session, out var rec))
            rec = GetOrCreate(session);

        rec.Cards.Add(OutputCard.Truncate(card));
        rec.LastActive = DateTimeOffset.UtcNow;
        AddHistoryEvent(session, "output_card", new { card.Title, card.Kind, card.Tags, card.CreatedUtc, card.Summary });
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

    public void AddHistoryEvent(string session, string type, object data)
    {
        if (!_sessions.TryGetValue(session, out var rec))
            rec = GetOrCreate(session);

        rec.History.Add(new SessionHistoryEvent(type, DateTimeOffset.UtcNow, data));
        rec.LastActive = DateTimeOffset.UtcNow;
        Persist(rec);
    }

    public PlanVersionSnapshot AddPlanVersion(string session, ToolPlan plan, string? planJson)
    {
        var steps = plan.Steps?.Select(s => new PlanVersionStep(s.Id, string.IsNullOrWhiteSpace(s.Why) ? s.ToolId : s.Why, s.ToolId, s.Why, s.Inputs)).ToList() ?? new List<PlanVersionStep>();
        return AddPlanVersion(session, steps, planJson);
    }

    public PlanVersionSnapshot AddPlanVersion(string session, IReadOnlyList<PlanVersionStep> steps, string? planJson)
    {
        if (!_sessions.TryGetValue(session, out var rec))
            rec = GetOrCreate(session);

        var version = rec.PlanVersions.Count + 1;
        var snapshot = new PlanVersionSnapshot(version, DateTimeOffset.UtcNow, steps.ToList(), planJson);
        rec.PlanVersions.Add(snapshot);
        rec.LastActive = DateTimeOffset.UtcNow;
        AddHistoryEvent(session, "plan", new { version, steps = steps.Select(s => s.Title).ToList() });
        Persist(rec);
        return snapshot;
    }

    public IReadOnlyList<PlanVersionSnapshot> GetPlanVersions(string session)
    {
        var rec = GetOrCreate(session);
        return rec.PlanVersions.ToList();
    }

    public object GetPlanDiff(string session, int from, int to)
    {
        var rec = GetOrCreate(session);
        var a = rec.PlanVersions.FirstOrDefault(p => p.Version == from);
        var b = rec.PlanVersions.FirstOrDefault(p => p.Version == to);
        if (a == null || b == null)
            return new { ok = false, error = "version_not_found" };

        var added = b.Steps.Where(step => a.Steps.All(p => !string.Equals(p.Id, step.Id, StringComparison.OrdinalIgnoreCase))).ToList();
        var removed = a.Steps.Where(step => b.Steps.All(p => !string.Equals(p.Id, step.Id, StringComparison.OrdinalIgnoreCase))).ToList();
        var changed = new List<object>();

        foreach (var step in b.Steps)
        {
            var prior = a.Steps.FirstOrDefault(p => string.Equals(p.Id, step.Id, StringComparison.OrdinalIgnoreCase));
            if (prior == null) continue;
            if (!string.Equals(prior.Title, step.Title, StringComparison.Ordinal) || !string.Equals(prior.ToolId, step.ToolId, StringComparison.OrdinalIgnoreCase) || !string.Equals(prior.Why, step.Why, StringComparison.Ordinal))
            {
                changed.Add(new { id = step.Id, fromStep = prior, toStep = step });
            }
        }

        return new
        {
            ok = true,
            from,
            to,
            added,
            removed,
            changed
        };
    }

    public SessionSnapshot Snapshot(string session)
    {
        var rec = GetOrCreate(session);
        return new SessionSnapshot(rec.Session, rec.StartedUtc, rec.LastActive, rec.Cards.ToList(), rec.Memory ?? new { }, rec.Artifacts.ToList(), rec.Logs.ToList(), rec.History.ToList(), rec.PlanVersions.ToList());
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

    public bool Terminate(string session)
    {
        if (!_sessions.TryRemove(session, out _))
            return false;

        var file = Path.Combine(_storageRoot, "session-" + session + ".json");
        if (File.Exists(file))
        {
            try { File.Delete(file); } catch { }
        }
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
                    Memory = rec.Memory,
                    History = rec.History?.ToList() ?? new List<SessionHistoryEvent>(),
                    PlanVersions = rec.PlanVersions?.ToList() ?? new List<PlanVersionSnapshot>()
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
                Memory = rec.Memory,
                History = rec.History,
                PlanVersions = rec.PlanVersions
            };
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(_storageRoot, "session-" + rec.Session + ".json"), json);
        }
        catch { }
    }

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _sessions.Values.Where(rec => now - rec.LastActive > _expiry).Select(r => r.Session).ToList();
        foreach (var id in expired)
            Terminate(id);
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
    public List<SessionHistoryEvent> History { get; set; } = new();
    public List<PlanVersionSnapshot> PlanVersions { get; set; } = new();

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
    public List<SessionHistoryEvent>? History { get; set; }
    public List<PlanVersionSnapshot>? PlanVersions { get; set; }
}
