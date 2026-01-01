#nullable enable
using System;
using System.Text.Json;

namespace RahBuilder.Workflow;

public static class JobSpecParser
{
    public static JobSpec? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = StripFences(text.Trim());

        // 1) Try parse as-is.
        if (TryParseToJobSpec(text, out var spec))
            return spec;

        // 2) If it wasn't clean JSON, extract first JSON object from surrounding junk.
        var extracted = ExtractFirstJsonObject(text);
        if (!string.IsNullOrWhiteSpace(extracted) && TryParseToJobSpec(extracted!, out spec))
            return spec;

        // 3) Last chance: sometimes the model returns a JSON STRING that contains JSON.
        // Example: "{\"intent\":\"...\"}"
        var unquoted = TryUnquoteJsonString(text);
        if (!string.IsNullOrWhiteSpace(unquoted) && TryParseToJobSpec(unquoted!, out spec))
            return spec;

        return null;
    }

    private static bool TryParseToJobSpec(string json, out JobSpec? spec)
    {
        spec = null;

        try
        {
            // Prefer JsonDocument parse first. If it parses, it's JSON.
            using var doc = JsonDocument.Parse(json);

            // If the root is a JSON string that contains JSON, unwrap it.
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                var inner = doc.RootElement.GetString() ?? "";
                if (inner.Length > 0)
                {
                    inner = inner.Trim();
                    using var innerDoc = JsonDocument.Parse(inner);
                    return TryBuildJobSpec(innerDoc, out spec);
                }
            }

            return TryBuildJobSpec(doc, out spec);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBuildJobSpec(JsonDocument doc, out JobSpec? spec)
    {
        spec = null;

        try
        {
            // If your JobSpec is a POCO, this will still work.
            // If it isn't, this won’t throw unless JobSpec is totally incompatible.
            var pojo = JsonSerializer.Deserialize<JobSpec>(
                doc.RootElement.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (pojo == null) return false;
            spec = pojo;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string StripFences(string s)
    {
        if (!s.StartsWith("```", StringComparison.Ordinal))
            return s;

        var firstNewline = s.IndexOf('\n');
        if (firstNewline >= 0)
            s = s[(firstNewline + 1)..];

        var endFence = s.LastIndexOf("```", StringComparison.Ordinal);
        if (endFence >= 0)
            s = s[..endFence];

        return s.Trim();
    }

    private static string? TryUnquoteJsonString(string s)
    {
        s = s.Trim();
        if (s.Length < 2) return null;
        if (s[0] != '"' || s[^1] != '"') return null;

        try
        {
            // Parse as JSON string to unescape properly.
            using var doc = JsonDocument.Parse(s);
            return doc.RootElement.ValueKind == JsonValueKind.String
                ? (doc.RootElement.GetString() ?? "").Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    // Balanced brace extraction for the first { ... } block.
    private static string? ExtractFirstJsonObject(string s)
    {
        int start = -1;
        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inString = false; continue; }
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
            {
                if (depth == 0) start = i;
                depth++;
                continue;
            }

            if (c == '}')
            {
                if (depth == 0) continue;
                depth--;
                if (depth == 0 && start >= 0)
                {
                    return s.Substring(start, i - start + 1).Trim();
                }
            }
        }

        return null;
    }
}
