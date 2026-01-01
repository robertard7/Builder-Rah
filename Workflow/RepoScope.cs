#nullable enable
using System;
using System.IO;
using RahBuilder.Settings;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public static class RepoScope
{
    public sealed record Result(bool Ok, string RepoRoot, string Message);
    public sealed record GitInfo(bool HasGit, string Message);

    public static Result Resolve(AppConfig cfg)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));

        var root = (cfg.General?.RepoRoot ?? "").Trim();
        if (string.IsNullOrWhiteSpace(root))
            return new Result(false, "", "[repo:error] RepoRoot is empty. Set Settings -> General.RepoRoot.");

        if (!Directory.Exists(root))
            return new Result(false, root, $"[repo:error] RepoRoot not found: {root}");

        return new Result(true, root, $"[repo:ok] RepoRoot={root}");
    }

    public static GitInfo CheckGit(Result scope, RunTrace? trace = null)
    {
        if (!scope.Ok)
        {
            var msg = "[git:skip] RepoRoot invalid; skipping git metadata check.";
            trace?.Emit(msg);
            return new GitInfo(false, msg);
        }

        var gitDir = Path.Combine(scope.RepoRoot, ".git");
        if (!Directory.Exists(gitDir))
        {
            var msg = "[git:skip] .git not found under RepoRoot; continuing without git metadata.";
            trace?.Emit(msg);
            return new GitInfo(false, msg);
        }

        var okMsg = "[git:ok] .git detected under RepoRoot.";
        trace?.Emit(okMsg);
        return new GitInfo(true, okMsg);
    }
}

