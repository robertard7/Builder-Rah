#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RahOllamaOnly.Tools.Prompts;

public sealed class PromptTemplateResolver
{
    private static readonly Regex TokenRegex =
        new(@"\{\{\s*(?<name>[A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.Compiled);

    public PromptResolutionResult Resolve(
        string template,
        IReadOnlyDictionary<string, string?> inputs,
        IReadOnlyCollection<string>? requiredInputs = null)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (inputs == null)
            throw new ArgumentNullException(nameof(inputs));

        var requiredSet = requiredInputs == null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(requiredInputs.Where(r => !string.IsNullOrWhiteSpace(r)), StringComparer.OrdinalIgnoreCase);

        var missingRequired = new HashSet<string>(
            requiredSet.Where(key => !inputs.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)),
            StringComparer.OrdinalIgnoreCase);

        var unresolvedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var resolved = TokenRegex.Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            if (inputs.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;

            unresolvedTokens.Add(name);
            return match.Value;
        });

        var missingList = missingRequired.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        var unresolvedList = unresolvedTokens.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        var fallback = missingList.Length > 0
            ? string.Format(CultureInfo.InvariantCulture, "Missing required inputs: {0}.", string.Join(", ", missingList))
            : null;

        return new PromptResolutionResult(resolved, missingList, unresolvedList, fallback);
    }
}

public sealed record PromptResolutionResult(
    string Prompt,
    IReadOnlyList<string> MissingRequiredInputs,
    IReadOnlyList<string> UnresolvedTokens,
    string? FallbackMessage)
{
    public bool Success => MissingRequiredInputs.Count == 0 && UnresolvedTokens.Count == 0;
}
