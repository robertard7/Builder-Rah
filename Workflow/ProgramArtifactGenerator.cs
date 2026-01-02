#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahOllamaOnly.Tools;

namespace RahBuilder.Workflow;

public sealed record ProgramArtifact(string Path, string Content, IReadOnlyList<string> Tags);
public sealed record ProgramArtifactResult(bool Ok, string Folder, string ZipPath, string Message, IReadOnlyList<ProgramArtifact> Files)
{
    public static ProgramArtifactResult Failure(string message) => new(false, "", "", message, Array.Empty<ProgramArtifact>());
    public static ProgramArtifactResult Success(string folder, string zipPath, IReadOnlyList<ProgramArtifact> files) => new(true, folder, zipPath, "ok", files);
}

public sealed class ProgramArtifactGenerator
{
    public async Task<ProgramArtifactResult> GenerateAsync(
        AppConfig cfg,
        IntentExtraction intent,
        IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments,
        string repoRoot,
        CancellationToken ct)
    {
        var prompt = (cfg.General.ProgramBuilderPrompt ?? "").Trim();
        if (prompt.Length == 0)
            return ProgramArtifactResult.Failure("program_prompt_missing");

        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            return ProgramArtifactResult.Failure("repo_root_invalid");

        var input = BuildInput(intent, attachments);
        string raw;
        try
        {
            raw = await LlmInvoker.InvokeChatAsync(cfg, "Orchestrator", prompt, input, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ProgramArtifactResult.Failure("program_invoke_failed: " + ex.Message);
        }

        var artifacts = ParseArtifacts(raw);
        if (artifacts.Count == 0)
            return ProgramArtifactResult.Failure("program_invalid_json");

        var targetRoot = Path.Combine(repoRoot, "Workflow", "ProgramArtifacts", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(targetRoot);

        foreach (var file in artifacts)
        {
            var safePath = Sanitize(file.Path);
            if (safePath.Length == 0) continue;
            var fullPath = Path.Combine(targetRoot, safePath.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, file.Content ?? "");
        }

        var zipPath = Path.Combine(Path.GetDirectoryName(targetRoot) ?? targetRoot, Path.GetFileName(targetRoot) + ".zip");
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        ZipFile.CreateFromDirectory(targetRoot, zipPath);

        return ProgramArtifactResult.Success(targetRoot, zipPath, artifacts);
    }

    private static string BuildInput(IntentExtraction intent, IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments)
    {
        var goal = intent?.Goal ?? "";
        var actions = intent?.Actions ?? Array.Empty<string>();
        var constraints = intent?.Constraints ?? Array.Empty<string>();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("GOAL:" + goal);
        sb.AppendLine("ACTIONS:" + string.Join(", ", actions));
        sb.AppendLine("CONSTRAINTS:" + string.Join(", ", constraints));
        sb.AppendLine("ATTACHMENTS:");
        foreach (var a in attachments.Where(a => a != null))
            sb.AppendLine($"- {a.StoredName} ({a.Kind})");
        return sb.ToString();
    }

    private static List<ProgramArtifact> ParseArtifacts(string raw)
    {
        var list = new List<ProgramArtifact>();
        if (string.IsNullOrWhiteSpace(raw))
            return list;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return list;

            var files = new List<ProgramArtifact>();
            if (root.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in filesEl.EnumerateArray())
                {
                    if (f.ValueKind != JsonValueKind.Object) continue;
                    var path = f.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
                    var content = f.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
                    var tags = new List<string>();
                    if (f.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var t in tagsEl.EnumerateArray())
                        {
                            if (t.ValueKind == JsonValueKind.String)
                            {
                                var s = t.GetString();
                                if (!string.IsNullOrWhiteSpace(s))
                                    tags.Add(s.Trim());
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(path))
                        files.Add(new ProgramArtifact(path.Trim(), content, tags));
                }
            }

            // Optional README/test fallbacks if not provided
            if (files.All(f => !string.Equals(Path.GetFileName(f.Path), "README.md", StringComparison.OrdinalIgnoreCase)))
                files.Add(new ProgramArtifact("README.md", "Project overview and usage will appear here.", new[] { "readme" }));

            if (files.Count == 0 && root.TryGetProperty("readme", out var readme) && readme.ValueKind == JsonValueKind.String)
                files.Add(new ProgramArtifact("README.md", readme.GetString() ?? "", new[] { "readme" }));

            return files;
        }
        catch
        {
            return list;
        }
    }

    private static string Sanitize(string path)
    {
        path ??= "";
        var cleaned = path.Replace("\\", "/").Trim();
        if (cleaned.StartsWith("..")) return "";
        if (cleaned.Contains("../", StringComparison.Ordinal) || cleaned.Contains("/..", StringComparison.Ordinal))
            return "";
        return cleaned;
    }
}
