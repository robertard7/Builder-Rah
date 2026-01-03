#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RahBuilder.Workflow;

public record SessionEvent(string Type, object Data, DateTimeOffset Timestamp);
public static class SessionEvents
{
    public static event Action<string, SessionEvent>? EventAdded;

    public static void Notify(string sessionId, SessionEvent evt)
    {
        try { EventAdded?.Invoke(sessionId, evt); } catch { }
    }
}

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

public sealed class SessionRecord
{
    public string SessionId { get; init; } = "";
    public DateTimeOffset CreatedUtc { get; init; }
    public DateTimeOffset LastActiveUtc { get; set; }
    public List<SessionEvent> History { get; init; } = new();
    public List<OutputCard> Cards { get; init; } = new();
    public List<string> Artifacts { get; init; } = new();
    public List<string> Logs { get; init; } = new();
    public object Memory { get; set; } = new();
    public List<PlanVersionSnapshot> PlanVersions { get; init; } = new();
}

public static class SessionManager
{
    private static readonly ConcurrentDictionary<string, SessionRecord> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "SessionData");
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(12);
    private static readonly object FileLock = new();
    private static readonly JsonSerializerOptions StorageOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static SessionManager()
    {
        Directory.CreateDirectory(Root);
        Load();
        PurgeExpired();
    }

    private static string GetPath(string id) => Path.Combine(Root, id + ".json");

    private static void Load()
    {
        try
        {
            foreach (var f in Directory.GetFiles(Root, "*.json"))
            {
                SessionRecord? rec;
                lock (FileLock)
                {
                    rec = JsonSerializer.Deserialize<SessionRecord>(File.ReadAllText(f), StorageOptions);
                }
                if (rec != null && !string.IsNullOrWhiteSpace(rec.SessionId))
                {
                    NormalizeRecord(rec);
                    _sessions[rec.SessionId] = rec;
                }
            }
        }
        catch { }
    }

    private static void Save(SessionRecord rec)
    {
        try
        {
            var path = GetPath(rec.SessionId);
            var tmp = path + ".tmp";
            var json = JsonSerializer.Serialize(rec, StorageOptions);
            lock (FileLock)
            {
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
        }
        catch { }
    }

    private static void NormalizeRecord(SessionRecord rec)
    {
        rec.LastActiveUtc = rec.LastActiveUtc == default ? rec.CreatedUtc : rec.LastActiveUtc;
        rec.History.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        RebuildDerivedState(rec);
    }

    private static void RebuildDerivedState(SessionRecord rec)
    {
        rec.Cards.Clear();
        rec.Logs.Clear();
        rec.Artifacts.Clear();
        rec.PlanVersions.Clear();

        foreach (var ev in rec.History.OrderBy(h => h.Timestamp))
        {
            ApplyEventToRecord(rec, ev);
        }

        rec.LastActiveUtc = rec.History.LastOrDefault()?.Timestamp ?? rec.LastActiveUtc;
    }

    private static SessionRecord GetOrCreate(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Create();

        return _sessions.GetOrAdd(id, key =>
        {
            var rec = new SessionRecord { SessionId = key, CreatedUtc = DateTimeOffset.UtcNow, LastActiveUtc = DateTimeOffset.UtcNow };
            Save(rec);
            return rec;
        });
    }

    public static SessionRecord Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var rec = new SessionRecord { SessionId = id, CreatedUtc = DateTimeOffset.UtcNow, LastActiveUtc = DateTimeOffset.UtcNow };
        _sessions[id] = rec;
        Save(rec);
        return rec;
    }

    public static string CreateSession() => Create().SessionId;

    public static bool Exists(string id) => !string.IsNullOrWhiteSpace(id) && _sessions.ContainsKey(id);

    public static bool TryGet(string id, out SessionRecord rec)
    {
        if (Exists(id))
        {
            rec = _sessions[id];
            return true;
        }

        rec = null!;
        return false;
    }

    public static IEnumerable<string> List() => _sessions.Keys;

    public static IReadOnlyList<SessionSnapshot> ListSessions()
    {
        PurgeExpired();
        return _sessions.Values.Select(SnapshotForRecord).ToList();
    }

    public static void AddEvent(string id, string type, object data)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var rec = GetOrCreate(id);
        var evt = new SessionEvent(type, data, DateTimeOffset.UtcNow);
        rec.History.Add(evt);
        ApplyEventToRecord(rec, evt);
        rec.LastActiveUtc = DateTimeOffset.UtcNow;
        Save(rec);
        SessionEvents.Notify(id, evt);
    }

    public static IReadOnlyList<SessionEvent> GetHistory(string id)
    {
        return TryGet(id, out var rec) ? rec.History.OrderBy(h => h.Timestamp).ToList() : new List<SessionEvent>();
    }

    public static void AppendCard(string session, OutputCard card)
    {
        if (card == null) return;
        AddEvent(session, "output_card", OutputCard.Truncate(card));
    }

    public static void AppendLog(string session, string log)
    {
        if (string.IsNullOrWhiteSpace(log)) return;
        AddEvent(session, "log", new { message = log.Trim(), timestamp = DateTimeOffset.UtcNow });
    }

    public static void UpdateMemory(string session, object memory)
    {
        var rec = GetOrCreate(session);
        rec.Memory = memory;
        rec.LastActiveUtc = DateTimeOffset.UtcNow;
        Save(rec);
    }

    public static void UpdateArtifacts(string session, IReadOnlyList<string> artifacts)
    {
        var list = artifacts?.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList() ?? new List<string>();
        AddEvent(session, "artifacts_updated", list);
    }

    public static void AddHistoryEvent(string session, string type, object data) => AddEvent(session, type, data);

    public static PlanVersionSnapshot AddPlanVersion(string session, ToolPlan plan, string? planJson)
    {
        var steps = plan.Steps?.Select(s => new PlanVersionStep(s.Id, string.IsNullOrWhiteSpace(s.Why) ? s.ToolId : s.Why, s.ToolId, s.Why, s.Inputs)).ToList() ?? new List<PlanVersionStep>();
        return AddPlanVersion(session, steps, planJson);
    }

    public static PlanVersionSnapshot AddPlanVersion(string session, IReadOnlyList<PlanVersionStep> steps, string? planJson)
    {
        var rec = GetOrCreate(session);
        var version = rec.PlanVersions.Count + 1;
        var snapshot = new PlanVersionSnapshot(version, DateTimeOffset.UtcNow, steps.ToList(), planJson);
        AddEvent(session, "plan_updated", snapshot);
        return snapshot;
    }

    public static IReadOnlyList<PlanVersionSnapshot> GetPlanVersions(string session)
    {
        var rec = GetOrCreate(session);
        return rec.PlanVersions.OrderBy(p => p.Version).ToList();
    }

    public static object GetPlanDiff(string session, int from, int to)
    {
        var rec = GetOrCreate(session);
        var ordered = rec.PlanVersions.OrderBy(p => p.Version).ToList();
        var a = ordered.FirstOrDefault(p => p.Version == from);
        var b = ordered.FirstOrDefault(p => p.Version == to);
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

    public static SessionSnapshot Snapshot(string session) => SnapshotForRecord(GetOrCreate(session));

    public static bool TryGetSnapshot(string session, out SessionSnapshot snapshot)
    {
        snapshot = default!;
        if (string.IsNullOrWhiteSpace(session) || !_sessions.TryGetValue(session, out _))
            return false;

        snapshot = Snapshot(session);
        return true;
    }

    public static bool Reset(string session)
    {
        if (!_sessions.TryGetValue(session, out _))
            return false;

        var fresh = new SessionRecord { SessionId = session, CreatedUtc = DateTimeOffset.UtcNow, LastActiveUtc = DateTimeOffset.UtcNow };
        _sessions[session] = fresh;
        Save(fresh);
        return true;
    }

    public static bool Terminate(string session)
    {
        if (!_sessions.TryRemove(session, out _))
            return false;

        var file = GetPath(session);
        if (File.Exists(file))
        {
            try { File.Delete(file); } catch { }
        }
        return true;
    }

    private static SessionSnapshot SnapshotForRecord(SessionRecord rec)
    {
        return new SessionSnapshot(
            rec.SessionId,
            rec.CreatedUtc,
            rec.LastActiveUtc,
            rec.Cards.ToList(),
            rec.Memory ?? new { },
            rec.Artifacts.ToList(),
            rec.Logs.ToList(),
            rec.History.Select(h => new SessionHistoryEvent(h.Type, h.Timestamp, h.Data)).ToList(),
            rec.PlanVersions.ToList());
    }

    private static void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _sessions.Values.Where(rec => now - rec.LastActiveUtc > Expiry).Select(r => r.SessionId).ToList();
        foreach (var id in expired)
            Terminate(id);
    }

    private static bool TryConvert<T>(object? data, out T result)
    {
        switch (data)
        {
            case T typed:
                result = typed;
                return true;
            case JsonElement el:
                try
                {
                    var deserialized = el.Deserialize<T>(StorageOptions);
                    if (deserialized != null)
                    {
                        result = deserialized;
                        return true;
                    }
                }
                catch { }
                break;
        }

        result = default!;
        return false;
    }

    private static void ApplyEventToRecord(SessionRecord rec, SessionEvent evt)
    {
        switch (evt.Type)
        {
            case "output_card":
                if (TryConvert<OutputCard>(evt.Data, out var card) && card != null)
                    rec.Cards.Add(OutputCard.Truncate(card));
                break;
            case "log":
                var message = "";
                var timestamp = evt.Timestamp;
                if (TryConvert<string>(evt.Data, out var logText) && !string.IsNullOrWhiteSpace(logText))
                {
                    message = logText.Trim();
                }
                else if (evt.Data is JsonElement el && el.ValueKind == JsonValueKind.String)
                {
                    message = el.GetString() ?? "";
                }
                else if (TryConvert<Dictionary<string, object>>(evt.Data, out var obj))
                {
                    if (obj.TryGetValue("message", out var msg) && msg is string s)
                        message = s;
                    if (obj.TryGetValue("timestamp", out var ts) && ts is DateTimeOffset dto)
                        timestamp = dto;
                }
                else if (evt.Data is JsonElement elObj && elObj.ValueKind == JsonValueKind.Object)
                {
                    if (elObj.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                        message = msgProp.GetString() ?? "";
                    if (elObj.TryGetProperty("timestamp", out var tsProp) && tsProp.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                    {
                        if (tsProp.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(tsProp.GetString(), out var parsed))
                            timestamp = parsed;
                        else if (tsProp.ValueKind == JsonValueKind.Number && tsProp.TryGetInt64(out var unix))
                            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unix);
                    }
                }

                if (!string.IsNullOrWhiteSpace(message))
                    rec.Logs.Add($"[{timestamp:O}] {message.Trim()}");
                break;
            case "artifacts_updated":
                if (TryConvert<List<string>>(evt.Data, out var artifacts) && artifacts != null)
                {
                    rec.Artifacts.Clear();
                    rec.Artifacts.AddRange(artifacts.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()));
                }
                else if (TryConvert<string[]>(evt.Data, out var arr) && arr != null)
                {
                    rec.Artifacts.Clear();
                    rec.Artifacts.AddRange(arr.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()));
                }
                break;
            case "plan_updated":
                if (TryConvert<PlanVersionSnapshot>(evt.Data, out var snap) && snap != null)
                {
                    var existing = rec.PlanVersions.FirstOrDefault(p => p.Version == snap.Version);
                    if (existing != null)
                        rec.PlanVersions.Remove(existing);
                    rec.PlanVersions.Add(snap);
                }
                break;
        }
    }
}
