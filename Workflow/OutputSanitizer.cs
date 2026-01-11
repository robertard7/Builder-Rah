#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace RahBuilder.Workflow;

public static class OutputSanitizer
{
    private static readonly string[] ForbiddenTokens =
    {
        "WAIT_USER",
        "tooling warnings",
        "tooling warning",
        "tools invalid",
        "tools inactive"
    };

    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        var sanitized = input;
        foreach (var token in ForbiddenTokens)
        {
            if (sanitized.Contains(token))
                sanitized = sanitized.Replace(token, "", System.StringComparison.OrdinalIgnoreCase);
        }
        return string.Join(" ", sanitized.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public static IReadOnlyList<string> Forbidden => ForbiddenTokens;
}
