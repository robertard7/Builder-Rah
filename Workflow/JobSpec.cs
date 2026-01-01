#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RahBuilder.Workflow;

public sealed class JobSpec
{
    public JsonDocument Raw { get; }
    public bool Ready { get; }
    public IReadOnlyList<string> Missing { get; }

    public bool IsComplete => Ready && Missing.Count == 0;

    private JobSpec(JsonDocument raw, bool ready, List<string> missing)
    {
        Raw = raw;
        Ready = ready;
        Missing = missing;
    }

    public static JobSpec Invalid(string reason)
    {
        var doc = JsonDocument.Parse($@"{{""state"":{{""ready"":false,""missing"":[""{Escape(reason)}""]}}}}");
        return new JobSpec(doc, false, new List<string> { reason });
    }

    public List<string> GetMissingFields() => new List<string>(Missing);

    private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
}
