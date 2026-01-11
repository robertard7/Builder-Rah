#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RahBuilder.Workflow;

public sealed record ChatTurn(string Role, string Content, DateTimeOffset Timestamp);

public sealed class ChatMemory
{
    private readonly List<ChatTurn> _turns = new();

    public IReadOnlyList<ChatTurn> Turns => _turns;

    public void Add(string role, string content)
    {
        var r = (role ?? "").Trim();
        var c = (content ?? "").Trim();
        if (r.Length == 0 || c.Length == 0)
            return;

        _turns.Add(new ChatTurn(r, c, DateTimeOffset.UtcNow));
    }

    public void SetTurns(IEnumerable<ChatTurn> turns)
    {
        _turns.Clear();
        if (turns == null) return;
        _turns.AddRange(turns.Where(t => t != null));
    }

    public string Summarize(int maxTurns)
    {
        if (maxTurns <= 0) return "";
        var slice = _turns.Skip(Math.Max(0, _turns.Count - maxTurns)).ToList();
        if (slice.Count == 0) return "";

        return string.Join("\n", slice.Select(t => $"[{t.Timestamp:O}] {t.Role}: {t.Content}"));
    }
}
