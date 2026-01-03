#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahOllamaOnly.Tools;

namespace RahBuilder.Workflow;

public sealed record ProgramArtifact(string Path, string Content, IReadOnlyList<string> Tags);

public sealed record ProgramArtifactPreview(string Path, string Preview, int LineCount, IReadOnlyList<string> Tags, string ContentType);

public sealed record ProgramArtifactResult(
    bool Ok,
    string Folder,
    string ZipPath,
    string Message,
    IReadOnlyList<ProgramArtifact> Files,
    IReadOnlyList<ProgramArtifactPreview> Previews,
    string TreePreview,
    IReadOnlyDictionary<string, string> FirstLines)
{
    public static ProgramArtifactResult Failure(string message) => new(false, "", "", message, Array.Empty<ProgramArtifact>(), Array.Empty<ProgramArtifactPreview>(), "", new Dictionary<string, string>());
    public static ProgramArtifactResult Success(string folder, string zipPath, IReadOnlyList<ProgramArtifact> files, IReadOnlyList<ProgramArtifactPreview> previews, string treePreview, IReadOnlyDictionary<string, string> firstLines) => new(true, folder, zipPath, "ok", files, previews, treePreview, firstLines);
}

public sealed class ProgramArtifactGenerator
{
    public async Task<ProgramArtifactResult> GenerateAsync(
        AppConfig cfg,
        IntentExtraction intent,
        IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments,
        IReadOnlyList<System.Text.Json.JsonElement> toolOutputs,
        string repoRoot,
        string sessionToken,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            return ProgramArtifactResult.Failure("repo_root_invalid");

        var artifacts = ExtractArtifacts(toolOutputs);
        if (artifacts.Count == 0)
        {
            var prompt = (cfg.General.ProgramBuilderPrompt ?? "").Trim();
            if (prompt.Length == 0)
                return ProgramArtifactResult.Failure("program_prompt_missing");

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

            artifacts = ParseArtifacts(raw);
        }

        artifacts = EnsureScaffolding(artifacts, intent);
        if (artifacts.Count == 0)
            return ProgramArtifactResult.Failure("program_invalid_json");

        var targetRoot = BuildTargetFolder(repoRoot, sessionToken);

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

        var previews = BuildPreviews(artifacts);
        var tree = BuildTreePreview(artifacts);
        var firstLines = CollectFirstLines(artifacts);
        return ProgramArtifactResult.Success(targetRoot, zipPath, artifacts, previews, tree, firstLines);
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

    public static List<ProgramArtifact> ExtractArtifacts(IReadOnlyList<System.Text.Json.JsonElement> outputs)
    {
        var files = new List<ProgramArtifact>();
        foreach (var output in outputs ?? Array.Empty<System.Text.Json.JsonElement>())
        {
            if (output.ValueKind != JsonValueKind.Object)
                continue;

            if (output.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in filesEl.EnumerateArray())
                    AddFile(files, f);
            }

            if (output.TryGetProperty("generatedFiles", out var gen) && gen.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in gen.EnumerateArray())
                    AddFile(files, f);
            }

            if (output.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String && output.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
            {
                files.Add(new ProgramArtifact(p.GetString() ?? "", c.GetString() ?? "", ReadTags(output)));
            }
        }

        return files;

        static void AddFile(List<ProgramArtifact> list, JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return;
            var path = element.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
            var content = element.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
            var tags = ReadTags(element);

            if (!string.IsNullOrWhiteSpace(path))
                list.Add(new ProgramArtifact(path.Trim(), content, tags));
        }
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
                    AddFile(files, f);
            }

            if (files.Count == 0 && root.TryGetProperty("readme", out var readme) && readme.ValueKind == JsonValueKind.String)
                files.Add(new ProgramArtifact("README.md", readme.GetString() ?? "", new[] { "readme" }));

            return EnsureScaffolding(files, null);
        }
        catch
        {
            return list;
        }
    }

    private static string BuildTargetFolder(string repoRoot, string sessionToken)
    {
        var session = SanitizeSegment(sessionToken);
        if (session.Length == 0)
            session = "session";

        var targetRoot = Path.Combine(repoRoot, "Workflow", "ProgramArtifacts", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + session);
        Directory.CreateDirectory(targetRoot);
        return targetRoot;
    }

    public static string BuildTreePreview(IEnumerable<ProgramArtifact> files)
    {
        var root = new Node("");
        foreach (var file in files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
        {
            var parts = file.Path.Replace("\\", "/").Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var isFile = i == parts.Length - 1;
                if (!current.Children.TryGetValue(part, out var next))
                {
                    next = new Node(part) { IsFile = isFile };
                    current.Children[part] = next;
                }
                current = next;
            }
        }

        var sb = new StringBuilder();
        foreach (var child in root.Children.Values.OrderBy(c => c.IsFile).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            Render(child, 0, sb);

        return sb.ToString().TrimEnd();

        static void Render(Node node, int depth, StringBuilder sb)
        {
            sb.Append(' ', depth * 2);
            sb.AppendLine(node.Name + (node.IsFile ? string.Empty : "/"));
            foreach (var child in node.Children.Values.OrderBy(c => c.IsFile).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                Render(child, depth + 1, sb);
        }
    }

    private static IReadOnlyDictionary<string, string> CollectFirstLines(IEnumerable<ProgramArtifact> files)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var line = (file.Content ?? "").Split('\n', '\r').FirstOrDefault(l => l.Length > 0) ?? "";
            dict[file.Path] = line.Trim();
        }
        return dict;
    }

    private static List<ProgramArtifactPreview> BuildPreviews(IEnumerable<ProgramArtifact> files)
    {
        var list = new List<ProgramArtifactPreview>();
        foreach (var file in files)
        {
            var preview = BuildPreview(file.Content, 50);
            var lineCount = CountLines(file.Content);
            var contentType = InferContentType(file.Path);
            list.Add(new ProgramArtifactPreview(file.Path, preview, lineCount, file.Tags, contentType));
        }
        return list;
    }

    private static int CountLines(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;
        return content.Split('\n').Length;
    }

    private static string BuildPreview(string? content, int maxLines)
    {
        content ??= "";
        var lines = content.Split('\n');
        var previewLines = lines.Take(maxLines).ToList();
        var preview = string.Join("\n", previewLines);
        if (lines.Length > previewLines.Count)
            preview += "\n…";
        return preview;
    }

    private static string InferContentType(string path)
    {
        var ext = Path.GetExtension(path).Trim('.').ToLowerInvariant();
        return ext.Length == 0 ? "text" : ext;
    }

    private static List<ProgramArtifact> EnsureScaffolding(List<ProgramArtifact> files, IntentExtraction? intent)
    {
        files ??= new List<ProgramArtifact>();

        if (files.All(f => !string.Equals(Path.GetFileName(f.Path), "README.md", StringComparison.OrdinalIgnoreCase)))
        {
            var goal = intent?.Goal ?? intent?.Request ?? "";
            files.Add(new ProgramArtifact("README.md", $"# Project\n\n{goal}\n\nGenerated on {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.\n", new[] { "readme" }));
        }

        if (!files.Any(f => f.Path.Contains("test", StringComparison.OrdinalIgnoreCase) || f.Path.EndsWith(".spec", StringComparison.OrdinalIgnoreCase)))
        {
            files.Add(new ProgramArtifact(Path.Combine("tests", "placeholder.test"), "# Add tests here\n", new[] { "test" }));
        }

        if (!files.Any(f => f.Path.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase) || f.Path.EndsWith("config.json", StringComparison.OrdinalIgnoreCase)))
        {
            files.Add(new ProgramArtifact(Path.Combine("config", "appsettings.json"), "{\n  \"setting\": \"value\"\n}", new[] { "config" }));
        }

        return files;
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

    private static string SanitizeSegment(string? value)
    {
        value ??= "";
        var invalid = Path.GetInvalidFileNameChars();
        var filtered = new string(value.Where(ch => !invalid.Contains(ch)).ToArray());
        return filtered.Replace("/", "-").Replace("\\", "-").Trim();
    }

    private static List<string> ReadTags(JsonElement element)
    {
        var tags = new List<string>();
        if (element.ValueKind != JsonValueKind.Object)
            return tags;

        if (element.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
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
        return tags;
    }

    private sealed class Node
    {
        public string Name { get; }
        public bool IsFile { get; set; }
        public Dictionary<string, Node> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Node(string name) => Name = name;
    }
}
