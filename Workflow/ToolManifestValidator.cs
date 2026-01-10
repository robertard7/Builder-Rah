#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RahOllamaOnly.Tools;

namespace RahBuilder.Workflow;

public static class ToolManifestValidator
{
    private static readonly HashSet<int> SupportedVersions = new() { 3 };
    private static readonly Regex ToolIdPattern = new("^[a-z][a-z0-9]*(?:\\.[a-z0-9]+)*$", RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(ToolManifest manifest, string executionTarget)
    {
        var errors = new List<string>();
        if (manifest == null)
        {
            errors.Add("Tools manifest is null.");
            return errors;
        }

        if (!SupportedVersions.Contains(manifest.Version))
        {
            var supported = string.Join(", ", SupportedVersions.OrderBy(v => v));
            errors.Add($"Unsupported tools manifest version '{manifest.Version}'. Supported versions: {supported}.");
        }

        foreach (var tool in manifest.ToolsById.Values.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(tool.Id))
            {
                errors.Add("Tool entry missing required field: id.");
                continue;
            }

            if (!ToolIdPattern.IsMatch(tool.Id))
                errors.Add($"Tool id '{tool.Id}' is not valid. Expected lowercase segments like 'sys.info' or 'windows.sys.info'.");

            if (string.IsNullOrWhiteSpace(tool.Description))
                errors.Add($"Tool '{tool.Id}' missing required field: desc.");

            if (string.IsNullOrWhiteSpace(tool.Command))
                errors.Add($"Tool '{tool.Id}' missing required field: cmd.");

            if (!string.IsNullOrWhiteSpace(executionTarget) && tool.Targets != null && tool.Targets.Count > 0)
            {
                if (!tool.Targets.Any(t => string.Equals(t, executionTarget, StringComparison.OrdinalIgnoreCase)))
                {
                    var targetList = string.Join(", ", tool.Targets);
                    errors.Add($"Tool '{tool.Id}' not compatible with execution target '{executionTarget}'. Supported targets: {targetList}.");
                }
            }
        }

        return errors;
    }
}
