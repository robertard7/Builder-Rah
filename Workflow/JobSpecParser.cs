#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RahBuilder.Workflow;

public static class JobSpecParser
{
    public static JobSpec? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var json = text.Trim();
        return TryParseToJobSpec(json, out var spec) ? spec : null;
    }

    private static bool TryParseToJobSpec(string json, out JobSpec? spec)
    {
        spec = null;

        JsonDocument? doc = null;

        try
        {
            doc = JsonDocument.Parse(json);
            var ok = TryBuildJobSpec(doc, out spec);
            if (!ok)
            {
                doc.Dispose();
            }

            return ok;
        }
        catch
        {
            doc?.Dispose();
            return false;
        }
    }

    private static bool TryBuildJobSpec(JsonDocument doc, out JobSpec? spec)
    {
        spec = null;

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!doc.RootElement.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.Object)
            return false;

        if (!state.TryGetProperty("ready", out var readyEl) || readyEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;

        if (!state.TryGetProperty("missing", out var missingEl) || missingEl.ValueKind != JsonValueKind.Array)
            return false;

        var missing = new List<string>();
        foreach (var item in missingEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return false;

            missing.Add(item.GetString() ?? "");
        }

        spec = JobSpec.FromJson(doc, readyEl.GetBoolean(), missing);
        return true;
    }
}
