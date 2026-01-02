#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RahBuilder.Workflow;

public sealed class JobSpecAttachment
{
    public string StoredName { get; }
    public string Kind { get; }
    public string Status { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<string> Tools { get; }

    public JobSpecAttachment(string storedName, string kind, string status, string summary, List<string> tags, List<string> tools)
    {
        StoredName = storedName;
        Kind = kind;
        Status = status;
        Summary = summary;
        Tags = tags ?? new List<string>();
        Tools = tools ?? new List<string>();
    }
}

public sealed class JobSpec
{
    public JsonDocument Raw { get; }
    public string Mode { get; }
    public string Goal { get; }
    public string Context { get; }
    public IReadOnlyList<string> Actions { get; }
    public IReadOnlyList<string> Constraints { get; }
    public IReadOnlyList<JobSpecAttachment> Attachments { get; }
    public string? Clarification { get; }
    public bool Ready { get; }
    public IReadOnlyList<string> Missing { get; }

    public bool IsComplete => Ready && Missing.Count == 0;

    private JobSpec(
        JsonDocument raw,
        string mode,
        string goal,
        string context,
        List<string> actions,
        List<string> constraints,
        List<JobSpecAttachment> attachments,
        string? clarification,
        bool ready,
        List<string> missing)
    {
        Raw = raw;
        Mode = mode;
        Goal = goal;
        Context = context;
        Actions = actions;
        Constraints = constraints;
        Attachments = attachments;
        Clarification = clarification;
        Ready = ready;
        Missing = missing;
    }

    public static JobSpec Invalid(string reason)
    {
        var doc = JsonDocument.Parse($@"{{""mode"":""jobspec.v2"",""state"":{{""ready"":false,""missing"":[""{Escape(reason)}""]}}}}");
        return new JobSpec(doc, "jobspec.v2", "", "", new List<string>(), new List<string>(), new List<JobSpecAttachment>(), null, false, new List<string> { reason });
    }

    public static JobSpec FromJson(
        JsonDocument raw,
        string mode,
        string goal,
        string context,
        List<string> actions,
        List<string> constraints,
        List<JobSpecAttachment> attachments,
        string? clarification,
        bool ready,
        List<string> missing)
    {
        if (raw == null) throw new ArgumentNullException(nameof(raw));
        missing ??= new List<string>();
        actions ??= new List<string>();
        constraints ??= new List<string>();
        attachments ??= new List<JobSpecAttachment>();
        return new JobSpec(raw, mode, goal, context, actions, constraints, attachments, clarification, ready, missing);
    }

    public List<string> GetMissingFields() => new List<string>(Missing);

    private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
}
