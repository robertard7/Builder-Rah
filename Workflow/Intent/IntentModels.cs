#nullable enable
using System;
using System.Collections.Generic;

namespace RahBuilder.Workflow;

public sealed record Attachment(string Path, string Name, string Mime, long Size, byte[]? OptionalBytes);

public sealed record UserMessage(string Text, IReadOnlyList<Attachment> Attachments, DateTimeOffset CreatedUtc);

public enum IntentStatus
{
    Collecting,
    Clarifying,
    Ready
}

public sealed class IntentState
{
    public string SessionId { get; set; } = "";
    public string? CurrentGoal { get; set; }
    public Dictionary<string, string> Slots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<UserMessage> ContextWindow { get; set; } = new();
    public IntentStatus Status { get; set; } = IntentStatus.Collecting;
}

public sealed record IntentUpdate(
    IntentState State,
    string? UserFacingMessage,
    bool RequiresUserInput,
    string? NextQuestion);

public sealed record IntentResult(IntentState FinalState, JobSpec Job);
