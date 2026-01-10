#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
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
    IReadOnlyDictionary<string, string> FirstLines,
    string Hash)
{
    public static ProgramArtifactResult Failure(string message) => new(false, "", "", message, Array.Empty<ProgramArtifact>(), Array.Empty<ProgramArtifactPreview>(), "", new Dictionary<string, string>(), "");
    public static ProgramArtifactResult Success(string folder, string zipPath, IReadOnlyList<ProgramArtifact> files, IReadOnlyList<ProgramArtifactPreview> previews, string treePreview, IReadOnlyDictionary<string, string> firstLines, string hash) => new(true, folder, zipPath, "ok", files, previews, treePreview, firstLines, hash);
}

public sealed record ProgramArtifactCacheEntry(
    string Hash,
    string Folder,
    string ZipPath,
    string Tree,
    IReadOnlyDictionary<string, string> FirstLines,
    IReadOnlyList<ProgramArtifact> Files,
    IReadOnlyList<ProgramArtifactPreview> Previews,
    DateTimeOffset CreatedUtc);

public sealed class ProgramArtifactGenerator
{
    private const string ArtifactsRoot = "Workflow/ProgramArtifacts";
    private const string CacheFileName = "cache.json";

    public Task<ProgramArtifactResult> GenerateAsync(
        AppConfig cfg,
        IntentExtraction intent,
        IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments,
        IReadOnlyList<JsonElement> toolOutputs,
        string repoRoot,
        string sessionToken,
        CancellationToken ct)
    {
        var spec = intent == null
            ? JobSpec.Invalid("intent_missing")
            : JobSpec.FromJson(
                JsonDocument.Parse($@"{{""mode"":""jobspec.v2"",""request"":""{Escape(intent.Request)}"",""goal"":""{Escape(intent.Goal)}"",""context"":""{Escape(intent.Context)}"",""actions"":{JsonSerializer.Serialize(intent.Actions ?? Array.Empty<string>())},""constraints"":{JsonSerializer.Serialize(intent.Constraints ?? Array.Empty<string>())},""state"":{{""ready"":true,""missing"":[]}}}}"),
                "jobspec.v2",
                intent.Request,
                intent.Goal,
                intent.Context,
                intent.Actions?.ToList() ?? new List<string>(),
                intent.Constraints?.ToList() ?? new List<string>(),
                new List<JobSpecAttachment>(),
                "",
                true,
                new List<string>());

        return BuildProjectArtifacts(spec, toolOutputs, cfg, attachments, repoRoot, sessionToken, ct);
    }

    public async Task<ProgramArtifactResult> BuildProjectArtifacts(
        JobSpec spec,
        IReadOnlyList<JsonElement> toolOutputs,
        AppConfig cfg,
        IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments,
        string repoRoot,
        string sessionToken,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            return ProgramArtifactResult.Failure("repo_root_invalid");

        var hash = ComputeHash(spec, toolOutputs, attachments);
        var cached = TryGetCachedArtifacts(repoRoot, hash);
        if (cached != null)
            return cached;

        var artifacts = ExtractArtifacts(toolOutputs);
        if (artifacts.Count == 0)
        {
            var prompt = (cfg.General.ProgramBuilderPrompt ?? "").Trim();
            if (prompt.Length == 0)
                return ProgramArtifactResult.Failure("program_prompt_missing");

            var input = BuildPromptInput(spec, attachments);
            string raw;
            try
            {
                raw = await LlmInvoker.InvokeChatAsync(cfg, "Orchestrator", prompt, input, ct, Array.Empty<string>(), "program_artifacts").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is ProviderUnavailableException)
                    return ProgramArtifactResult.Failure("provider_unreachable");
                if (ex is ProviderDisabledException)
                    return ProgramArtifactResult.Failure("provider_disabled");
                return ProgramArtifactResult.Failure("program_invoke_failed: " + ex.Message);
            }

            artifacts = ParseArtifacts(raw);
        }

        artifacts = EnsureScaffolding(artifacts, spec);
        if (artifacts.Count == 0)
            return ProgramArtifactResult.Failure("program_invalid_json");

        var targetRoot = BuildTargetFolder(repoRoot, sessionToken, hash);
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

        var result = ProgramArtifactResult.Success(targetRoot, zipPath, artifacts, previews, tree, firstLines, hash);
        RegisterArtifacts(repoRoot, result);
        return result;
    }

    public ProgramArtifactResult? TryGetCachedArtifacts(string repoRoot, string hash)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || string.IsNullOrWhiteSpace(hash))
            return null;

        var cache = LoadCache(repoRoot);
        if (!cache.TryGetValue(hash, out var entry))
            return null;

        if (string.IsNullOrWhiteSpace(entry.ZipPath) || !File.Exists(entry.ZipPath))
            return null;

        if (string.IsNullOrWhiteSpace(entry.Folder) || !Directory.Exists(entry.Folder))
        {
            var tempExtract = Path.Combine(Path.GetDirectoryName(entry.ZipPath) ?? repoRoot, Path.GetFileNameWithoutExtension(entry.ZipPath));
            Directory.CreateDirectory(tempExtract);
            ZipFile.ExtractToDirectory(entry.ZipPath, tempExtract, true);
            entry = entry with { Folder = tempExtract };
        }

        return ProgramArtifactResult.Success(entry.Folder, entry.ZipPath, entry.Files, entry.Previews, entry.Tree, entry.FirstLines, entry.Hash);
    }

    public void RegisterArtifacts(string repoRoot, ProgramArtifactResult result)
    {
        if (!result.Ok || string.IsNullOrWhiteSpace(result.Hash))
            return;

        var cache = LoadCache(repoRoot);
        cache[result.Hash] = new ProgramArtifactCacheEntry(
            result.Hash,
            result.Folder,
            result.ZipPath,
            result.TreePreview,
            result.FirstLines,
            result.Files,
            result.Previews,
            DateTimeOffset.UtcNow);
        SaveCache(repoRoot, cache);
    }

    private static Dictionary<string, ProgramArtifactCacheEntry> LoadCache(string repoRoot)
    {
        var cachePath = GetCachePath(repoRoot);
        try
        {
            if (!File.Exists(cachePath))
                return new Dictionary<string, ProgramArtifactCacheEntry>(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(cachePath);
            var entries = JsonSerializer.Deserialize<List<ProgramArtifactCacheEntry>>(json) ?? new List<ProgramArtifactCacheEntry>();
            return entries.ToDictionary(e => e.Hash, e => e, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, ProgramArtifactCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveCache(string repoRoot, Dictionary<string, ProgramArtifactCacheEntry> cache)
    {
        var cachePath = GetCachePath(repoRoot);
        var dir = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(cache.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(cachePath, json);
    }

    private static string GetCachePath(string repoRoot) =>
        Path.Combine(repoRoot, ArtifactsRoot, "cache", CacheFileName);

    private static string BuildPromptInput(JobSpec spec, IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments)
    {
        var sb = new StringBuilder();
        sb.AppendLine("REQUEST:" + (spec?.Request ?? ""));
        sb.AppendLine("GOAL:" + (spec?.Goal ?? ""));
        sb.AppendLine("ACTIONS:" + string.Join(", ", spec?.Actions ?? Array.Empty<string>()));
        sb.AppendLine("CONSTRAINTS:" + string.Join(", ", spec?.Constraints ?? Array.Empty<string>()));
        sb.AppendLine("ATTACHMENTS:");
        foreach (var a in attachments.Where(a => a != null))
            sb.AppendLine($"- {a.StoredName} ({a.Kind})");
        return sb.ToString();
    }

    private static string ComputeHash(JobSpec spec, IReadOnlyList<JsonElement> outputs, IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments)
    {
        var sb = new StringBuilder();
        sb.Append(spec?.Request ?? "");
        sb.Append(spec?.Goal ?? "");
        sb.Append(spec?.Context ?? "");
        foreach (var a in spec?.Actions ?? Array.Empty<string>()) sb.Append(a);
        foreach (var c in spec?.Constraints ?? Array.Empty<string>()) sb.Append(c);
        foreach (var att in spec?.Attachments ?? Array.Empty<JobSpecAttachment>())
        {
            sb.Append(att.StoredName);
            sb.Append(att.Kind);
            sb.Append(att.Status);
            foreach (var t in att.Tags) sb.Append(t);
        }
        foreach (var a in attachments ?? Array.Empty<AttachmentInbox.AttachmentEntry>())
        {
            sb.Append(a.StoredName);
            sb.Append(a.Kind);
            sb.Append(a.SizeBytes);
            sb.Append(a.Sha256);
        }
        foreach (var output in outputs ?? Array.Empty<JsonElement>())
            sb.Append(output.GetRawText());

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = Convert.ToHexString(sha.ComputeHash(bytes));
        return hash;
    }

    public static List<ProgramArtifact> ExtractArtifacts(IReadOnlyList<JsonElement> outputs)
    {
        var files = new List<ProgramArtifact>();
        foreach (var output in outputs ?? Array.Empty<JsonElement>())
        {
            if (output.ValueKind != JsonValueKind.Object)
                continue;

            if (output.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in filesEl.EnumerateArray())
                    AddProgramFile(files, f);
            }

            if (output.TryGetProperty("generatedFiles", out var gen) && gen.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in gen.EnumerateArray())
                    AddProgramFile(files, f);
            }

            if (output.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String && output.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
            {
                files.Add(new ProgramArtifact(p.GetString() ?? "", c.GetString() ?? "", ReadTags(output)));
            }
        }

        return files;
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
                    AddProgramFile(files, f);
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

    private static string BuildTargetFolder(string repoRoot, string sessionToken, string hash)
    {
        var session = SanitizeSegment(sessionToken);
        if (session.Length == 0)
            session = "session";

        var targetRoot = Path.Combine(repoRoot, ArtifactsRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + session + "-" + hash[..8]);
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

    private static List<ProgramArtifact> EnsureScaffolding(List<ProgramArtifact> files, JobSpec? spec)
    {
        files ??= new List<ProgramArtifact>();

        if (files.All(f => !string.Equals(Path.GetFileName(f.Path), "README.md", StringComparison.OrdinalIgnoreCase)))
        {
            var goal = spec?.Goal ?? spec?.Request ?? "";
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

    private static void AddProgramFile(List<ProgramArtifact> list, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        var path = element.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
        var content = element.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
        var tags = ReadTags(element);

        if (!string.IsNullOrWhiteSpace(path))
            list.Add(new ProgramArtifact(path.Trim(), content, tags));
    }

    private static string Escape(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class Node
    {
        public string Name { get; }
        public bool IsFile { get; set; }
        public Dictionary<string, Node> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Node(string name) => Name = name;
    }
}
