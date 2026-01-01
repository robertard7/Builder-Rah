#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using RahBuilder.Settings;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public sealed class AttachmentInbox
{
    public sealed class AttachmentEntry
    {
        public string OriginalName { get; set; } = "";
        public string StoredName { get; set; } = "";
        public string HostPath { get; set; } = "";
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = "";
        public DateTime AddedUtc { get; set; }
        public string Kind { get; set; } = "other";
    }

    public sealed class AttachmentOperationResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public List<AttachmentEntry> Added { get; set; } = new();
    }

    private sealed class AttachmentManifest
    {
        public List<AttachmentEntry> Items { get; set; } = new();
    }

    private readonly object _lock = new();
    private readonly RunTrace? _trace;
    private readonly string _inboxPath;
    private readonly HashSet<string> _acceptedExts;
    private readonly long _maxAttachmentBytes;
    private readonly long _maxTotalInboxBytes;

    public AttachmentInbox(GeneralSettings general, RunTrace? trace = null)
    {
        _trace = trace;
        _inboxPath = (general?.InboxHostPath ?? "").Trim();
        if (string.IsNullOrWhiteSpace(_inboxPath))
            _inboxPath = Path.Combine(AppContext.BaseDirectory, "inbox");

        _acceptedExts = ParseExts(general?.AcceptedAttachmentExtensions);
        _maxAttachmentBytes = Math.Max(1, general?.MaxAttachmentBytes ?? (50 * 1024 * 1024));
        _maxTotalInboxBytes = Math.Max(1, general?.MaxTotalInboxBytes ?? (500 * 1024 * 1024));

        EnsureInbox();
    }

    public string InboxPath => _inboxPath;

    public IReadOnlyList<AttachmentEntry> List() => LoadManifest().Items;

    public AttachmentOperationResult AddFiles(IEnumerable<string> filePaths)
    {
        var result = new AttachmentOperationResult();
        var files = (filePaths ?? Array.Empty<string>()).Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
        if (files.Count == 0)
        {
            result.Ok = false;
            result.Message = "No files provided.";
            return result;
        }

        lock (_lock)
        {
            var manifest = LoadManifest();
            var totalSize = manifest.Items.Sum(i => i.SizeBytes);

            foreach (var file in files)
            {
                if (!File.Exists(file))
                {
                    return Fail(result, $"File not found: {file}");
                }

                var info = new FileInfo(file);
                if (info.Length > _maxAttachmentBytes)
                    return Fail(result, $"Attachment exceeds per-file limit ({FormatBytes(_maxAttachmentBytes)}).");

                if (totalSize + info.Length > _maxTotalInboxBytes)
                    return Fail(result, $"Total inbox size would exceed limit ({FormatBytes(_maxTotalInboxBytes)}).");

                var ext = (Path.GetExtension(file) ?? "").ToLowerInvariant();
                if (_acceptedExts.Count > 0 && !_acceptedExts.Contains(ext))
                    return Fail(result, $"Extension not allowed: {ext}");

                var storedName = NextStoredName(ext);
                var hostPath = Path.Combine(_inboxPath, storedName);

                var entry = CopyAndHash(file, hostPath, storedName);
                entry.OriginalName = Path.GetFileName(file) ?? storedName;
                entry.Kind = DetermineKind(ext);
                entry.AddedUtc = DateTime.UtcNow;

                manifest.Items.Add(entry);
                totalSize += entry.SizeBytes;
                result.Added.Add(entry);
            }

            SaveManifest(manifest);
            result.Ok = true;
            result.Message = "Added";
            return result;
        }
    }

    public AttachmentOperationResult Remove(string storedName)
    {
        var result = new AttachmentOperationResult();
        if (string.IsNullOrWhiteSpace(storedName))
            return Fail(result, "storedName required");

        lock (_lock)
        {
            var manifest = LoadManifest();
            var match = manifest.Items.FirstOrDefault(i => string.Equals(i.StoredName, storedName, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                return Fail(result, "Attachment not found");

            var hostPath = match.HostPath;
            try
            {
                if (!string.IsNullOrWhiteSpace(hostPath) && File.Exists(hostPath))
                    File.Delete(hostPath);
            }
            catch (Exception ex)
            {
                _trace?.Emit("[inbox:error] failed to delete file: " + ex.Message);
            }

            manifest.Items.Remove(match);
            SaveManifest(manifest);

            result.Ok = true;
            result.Message = "Removed";
            result.Added.Add(match);
            return result;
        }
    }

    private void EnsureInbox()
    {
        try
        {
            Directory.CreateDirectory(_inboxPath);
        }
        catch (Exception ex)
        {
            _trace?.Emit("[inbox:error] " + ex.Message);
        }
    }

    private AttachmentManifest LoadManifest()
    {
        EnsureInbox();
        var path = ManifestPath();

        try
        {
            if (!File.Exists(path))
                return new AttachmentManifest();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AttachmentManifest>(json, JsonOptions()) ?? new AttachmentManifest();
        }
        catch (Exception ex)
        {
            _trace?.Emit("[inbox:error] failed to read manifest: " + ex.Message);
            return new AttachmentManifest();
        }
    }

    private void SaveManifest(AttachmentManifest manifest)
    {
        EnsureInbox();
        var path = ManifestPath();
        try
        {
            var json = JsonSerializer.Serialize(manifest, JsonOptions());
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _trace?.Emit("[inbox:error] failed to write manifest: " + ex.Message);
        }
    }

    private AttachmentEntry CopyAndHash(string source, string dest, string storedName)
    {
        using var sha = SHA256.Create();

        Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? _inboxPath);

        using var src = File.OpenRead(source);
        using var dst = File.Create(dest);

        var buffer = new byte[81920];
        int read;
        long total = 0;
        while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
        {
            dst.Write(buffer, 0, read);
            sha.TransformBlock(buffer, 0, read, null, 0);
            total += read;
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        return new AttachmentEntry
        {
            StoredName = storedName,
            HostPath = dest,
            SizeBytes = total,
            Sha256 = Convert.ToHexString(sha.Hash ?? Array.Empty<byte>())
        };
    }

    private string ManifestPath() => Path.Combine(_inboxPath, "attachments.manifest.json");

    private static string DetermineKind(string ext)
    {
        ext = (ext ?? "").ToLowerInvariant();
        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp") return "image";
        if (ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz") return "archive";
        if (ext is ".txt" or ".md" or ".json" or ".pdf" or ".doc" or ".docx") return "document";
        return "other";
    }

    private string NextStoredName(string ext)
    {
        var safeExt = (ext ?? "").Trim();
        for (var i = 0; i < 5; i++)
        {
            var candidate = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{safeExt}";
            var path = Path.Combine(_inboxPath, candidate);
            if (!File.Exists(path))
                return candidate;
        }

        return $"{Guid.NewGuid():N}{safeExt}";
    }

    private static HashSet<string> ParseExts(string? exts)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(exts)) return set;

        var parts = exts.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var e = p.StartsWith('.') ? p : "." + p;
            set.Add(e.ToLowerInvariant());
        }

        return set;
    }

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        if (bytes >= GB) return $"{bytes / (double)GB:0.##} GB";
        if (bytes >= MB) return $"{bytes / (double)MB:0.##} MB";
        if (bytes >= KB) return $"{bytes / (double)KB:0.##} KB";
        return bytes + " B";
    }

    private static AttachmentOperationResult Fail(AttachmentOperationResult result, string message)
    {
        result.Ok = false;
        result.Message = message;
        return result;
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
