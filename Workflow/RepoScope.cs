#nullable enable
using System;
using System.IO;
using System.Diagnostics;
using RahBuilder.Settings;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public static class RepoScope
{
    public sealed record Result(bool Ok, string RepoRoot, string GitTopLevel, string Message);
    public sealed record GitInfo(bool HasGit, string Message);

    public static Result Resolve(AppConfig cfg)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));

        var root = PickRoot(cfg);
        var gitTop = ResolveGitTopLevel(root);

        if (string.IsNullOrWhiteSpace(root))
            return new Result(false, "", gitTop, "[repo:error] RepoRoot is empty. Set Settings -> General.RepoRoot.");

        if (!Directory.Exists(root))
            return new Result(false, root, gitTop, $"[repo:error] RepoRoot not found: {root}");

        if (!string.IsNullOrWhiteSpace(gitTop) &&
            !string.Equals(Path.GetFullPath(gitTop), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
        {
            return new Result(false, root, gitTop, $"[repo:error] RepoRoot {root} != git toplevel {gitTop}");
        }

        return new Result(true, root, gitTop, $"[repo:ok] RepoRoot={root} GitTopLevel={(string.IsNullOrWhiteSpace(gitTop) ? "n/a" : gitTop)}");
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

    private static string PickRoot(AppConfig cfg)
    {
        var root = (cfg.General?.RepoRoot ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            return root;

        var envRoot = Environment.GetEnvironmentVariable("REPO_ROOT") ?? "";
        if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
            return envRoot.Trim();

        var envRah = Environment.GetEnvironmentVariable("RAH_REPO_DIR") ?? "";
        if (!string.IsNullOrWhiteSpace(envRah) && Directory.Exists(envRah))
            return envRah.Trim();

        var gitTop = ResolveGitTopLevel("");
        if (!string.IsNullOrWhiteSpace(gitTop))
            return gitTop;

        return root;
    }

    private static string ResolveGitTopLevel(string rootHint)
    {
        try
        {
            var startInfo = new ProcessStartInfo("git", "rev-parse --show-toplevel")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (!string.IsNullOrWhiteSpace(rootHint) && Directory.Exists(rootHint))
                startInfo.WorkingDirectory = rootHint;

            using var p = Process.Start(startInfo);
            if (p == null) return "";
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(2000);
            if (p.ExitCode != 0) return "";
            return output;
        }
        catch
        {
            return "";
        }
    }
}
