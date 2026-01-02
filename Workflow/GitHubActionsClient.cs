#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public sealed class GitHubActionsClient
{
    private readonly string _repository;
    private readonly HttpClient _http;
    private readonly RunTrace _trace;

    public GitHubActionsClient(string repository, string token, RunTrace trace)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));

        if (string.IsNullOrWhiteSpace(_repository))
            throw new ArgumentException("Repository is required.", nameof(repository));

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("GitHub token is required.", nameof(token));

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("RahBuilder/1.0");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    private string BaseUrl => $"https://api.github.com/repos/{_repository}";

    public async Task<bool> DispatchAsync(string workflowFile, string @ref, CancellationToken ct)
    {
        var payload = new { @ref };
        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/actions/workflows/{workflowFile}/dispatches")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _trace.Emit($"[gh:dispatch:error] HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} {body}");
            return false;
        }

        _trace.Emit("[gh:dispatch] workflow_dispatch accepted.");
        return true;
    }

    public async Task<WorkflowRunInfo?> FindLatestRunAsync(string workflowFile, DateTimeOffset since, CancellationToken ct)
    {
        var url = $"{BaseUrl}/actions/workflows/{workflowFile}/runs?per_page=5&event=workflow_dispatch";
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _trace.Emit($"[gh:runs:error] HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} {body}");
            return null;
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("workflow_runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var run in runs.EnumerateArray())
        {
            var created = run.TryGetProperty("created_at", out var ca) && DateTimeOffset.TryParse(ca.GetString(), out var createdAt)
                ? createdAt
                : DateTimeOffset.MinValue;

            if (created < since)
                continue;

            var info = ToRunInfo(run);
            if (info != null)
                return info;
        }

        return null;
    }

    public async Task<WorkflowRunInfo?> GetRunAsync(long runId, CancellationToken ct)
    {
        using var resp = await _http.GetAsync($"{BaseUrl}/actions/runs/{runId}", ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return ToRunInfo(doc.RootElement);
    }

    public async Task<IReadOnlyList<string>> DownloadLogsAsync(long runId, CancellationToken ct)
    {
        using var resp = await _http.GetAsync($"{BaseUrl}/actions/runs/{runId}/logs", ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _trace.Emit($"[gh:logs:error] HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} {body}");
            return Array.Empty<string>();
        }

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var result = new List<string>();
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                continue;

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            var text = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            result.Add($"== {entry.FullName} ==\n{text}");
        }

        return result;
    }

    private static WorkflowRunInfo? ToRunInfo(JsonElement el)
    {
        if (!el.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
            return null;

        var id = idEl.GetInt64();
        var status = el.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
        var conclusion = el.TryGetProperty("conclusion", out var c) ? c.GetString() ?? "" : "";
        var html = el.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
        var logs = el.TryGetProperty("logs_url", out var l) ? l.GetString() ?? "" : "";

        return new WorkflowRunInfo(id, status, conclusion, html, logs);
    }
}

public sealed record WorkflowRunInfo(long Id, string Status, string Conclusion, string HtmlUrl, string LogsUrl)
{
    public bool IsCompleted => string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase);
}
