#nullable enable
using System;
using System.Collections.Generic;

namespace RahBuilder.Workflow;

public sealed record SessionMessage(string Sender, string Text, DateTimeOffset Timestamp);

public sealed record SessionAttachment(
    string StoredName,
    string OriginalName,
    string Kind,
    long SizeBytes,
    string Sha256,
    bool Active);

public sealed class SessionState
{
    public string SessionId { get; set; } = "";
    public List<SessionMessage> Messages { get; set; } = new();
    public List<SessionAttachment> Attachments { get; set; } = new();
    public IntentState Intent { get; set; } = new();
    public string LastJobSpecJson { get; set; } = "";
    public string PendingToolPlanJson { get; set; } = "";
    public int PendingStepIndex { get; set; }
    public bool PlanConfirmed { get; set; }
    public bool Canceled { get; set; }
    public DateTimeOffset LastTouched { get; set; } = DateTimeOffset.UtcNow;
}
