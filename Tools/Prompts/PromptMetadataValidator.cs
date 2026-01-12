#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RahOllamaOnly.Tools.Prompts;

public static class PromptMetadataValidator
{
    public static void EnsureValid(PromptMetadata metadata)
    {
        var errors = Validate(metadata);
        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }

    public static IReadOnlyList<string> Validate(PromptMetadata metadata)
    {
        var errors = new List<string>();
        if (metadata == null)
        {
            errors.Add("Prompt metadata is null.");
            return errors;
        }

        if (metadata.Version <= 0)
            errors.Add("Prompt metadata version must be greater than zero.");

        if (string.IsNullOrWhiteSpace(metadata.Description))
            errors.Add("Prompt metadata description is required.");

        if (metadata.RequiredFields == null || metadata.RequiredFields.Count == 0)
            errors.Add("Prompt metadata requiredFields must include at least one entry.");
        else
        {
            var normalized = new HashSet<string>(metadata.RequiredFields, StringComparer.OrdinalIgnoreCase);
            var missing = new[] { "version", "description", "tags", "category" }
                .Where(required => !normalized.Contains(required))
                .ToArray();
            if (missing.Length > 0)
                errors.Add($"Prompt metadata requiredFields missing: {string.Join(", ", missing)}.");
        }

        if (metadata.Categories == null || metadata.Categories.Count == 0)
            errors.Add("Prompt metadata categories must include at least one entry.");
        else if (metadata.Categories.Any(category => string.IsNullOrWhiteSpace(category)))
            errors.Add("Prompt metadata categories cannot contain empty values.");

        if (metadata.TagHints == null || metadata.TagHints.Count == 0)
            errors.Add("Prompt metadata tagHints must include at least one entry.");
        else if (metadata.TagHints.Any(tag => string.IsNullOrWhiteSpace(tag)))
            errors.Add("Prompt metadata tagHints cannot contain empty values.");

        return errors;
    }
}
