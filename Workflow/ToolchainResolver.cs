#nullable enable
using System;
using System.IO;
using RahBuilder.Settings;

namespace RahBuilder.Workflow;

public static class ToolchainResolver
{
    public static string ResolveToolManifestPath(AppConfig cfg) =>
        ResolveToolManifestPath(cfg?.General?.RepoRoot ?? "", cfg?.General?.ExecutionTarget ?? "");

    public static string ResolveToolManifestPath(string repoRoot, string executionTarget)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
            return "";

        var toolsDir = Path.Combine(repoRoot, "Tools");
        var file = IsLinuxTarget(executionTarget) ? "linux.tools.json" : "windows.tools.json";
        return Path.Combine(toolsDir, file);
    }

    public static string ResolveToolPromptsFolder(AppConfig cfg) =>
        ResolveToolPromptsFolder(cfg?.General?.RepoRoot ?? "", cfg?.General?.ExecutionTarget ?? "");

    public static string ResolveToolPromptsFolder(string repoRoot, string executionTarget)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
            return "";

        var toolsDir = Path.Combine(repoRoot, "Tools");
        var targetFolder = IsLinuxTarget(executionTarget) ? "Prompt.Linux" : "Prompt.Windows";
        var targetPath = Path.Combine(toolsDir, targetFolder);
        if (Directory.Exists(targetPath))
            return targetPath;

        return Path.Combine(toolsDir, "Prompt");
    }

    private static bool IsLinuxTarget(string executionTarget) =>
        string.Equals(executionTarget?.Trim(), "LinuxContainer", StringComparison.OrdinalIgnoreCase);
}
