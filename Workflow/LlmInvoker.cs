#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;

namespace RahOllamaOnly.Tools;

public static class LlmInvoker
{
    private static readonly HttpClient _http = new HttpClient();
    public static Action<string>? AuditLogger { get; set; }

    public static async Task<string> InvokeChatAsync(
        AppConfig cfg,
        string role,
        string systemPrompt,
        string userText,
        CancellationToken ct,
        IEnumerable<string>? blueprintIds = null,
        string? reason = null)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        role = (role ?? "").Trim();
        if (role.Length == 0) throw new InvalidOperationException("Role is required.");

        var rc = cfg.Orchestrator?.Roles?
            .FirstOrDefault(r => string.Equals(r.Role ?? "", role, StringComparison.OrdinalIgnoreCase));

        if (rc == null)
            throw new InvalidOperationException($"[digest:error] Role '{role}' is not configured (Orchestrator.Roles).");

        var provider = (rc.Provider ?? "").Trim();
        var model = (rc.Model ?? "").Trim();

        if (model.Length == 0)
            throw new InvalidOperationException($"[digest:error] {provider} model is not set for role '{role}' (Orchestrator.Roles[*].Model).");

        try
        {
            var payload = new
            {
                mode = "audit.model_call.v1",
                role,
                provider,
                model,
                selectedBlueprintIDs = blueprintIds?.ToArray() ?? Array.Empty<string>(),
                reasonSummary = reason ?? ""
            };
            AuditLogger?.Invoke(JsonSerializer.Serialize(payload));
        }
        catch
        {
        }

        if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            return await InvokeOllamaAsync(cfg, model, systemPrompt, userText, ct).ConfigureAwait(false);

        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            return await InvokeOpenAiAsync(cfg, model, systemPrompt, userText, ct).ConfigureAwait(false);

        if (provider.Equals("HuggingFace", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("Hugging Face", StringComparison.OrdinalIgnoreCase))
            return await InvokeHuggingFaceAsync(cfg, model, systemPrompt, userText, ct).ConfigureAwait(false);

        throw new InvalidOperationException($"[digest:error] Unsupported provider '{provider}' for role '{role}'.");
    }

	private static async Task<string> InvokeOllamaAsync(
		AppConfig cfg,
		string model,
		string systemPrompt,
		string userText,
		CancellationToken ct)
	{
		if (cfg.Providers?.Ollama?.Enabled != true)
			throw new InvalidOperationException("[digest:error] Ollama provider is disabled.");

		var baseUrl = (cfg.Providers.Ollama.BaseUrl ?? "").Trim().TrimEnd('/');
		if (baseUrl.Length == 0)
			throw new InvalidOperationException("[digest:error] Ollama BaseUrl is empty (Providers.Ollama.BaseUrl).");

		// Use /api/generate for deterministic single-shot structured output.
		var url = baseUrl + "/api/generate";

		var combinedPrompt =
			(systemPrompt ?? "").Trim() +
			"\n\nUSER REQUEST:\n" +
			(userText ?? "").Trim() +
			"\n\nReturn EXACTLY one JSON object and nothing else.";

		var payload = new
		{
			model,
			prompt = combinedPrompt,
			stream = false,
			format = "json",
			options = new
			{
				temperature = 0.0
			}
		};

		var reqJson = JsonSerializer.Serialize(payload);

		using var req = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = new StringContent(reqJson, Encoding.UTF8, "application/json")
		};

		using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
		var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

		if (!resp.IsSuccessStatusCode)
			throw new InvalidOperationException($"[digest:error] Ollama /api/generate HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

		// Ollama generate response: { response: "....", done: true, ... }
		try
		{
			using var doc = JsonDocument.Parse(body);
			if (doc.RootElement.TryGetProperty("response", out var r) &&
				r.ValueKind == JsonValueKind.String)
			{
				return r.GetString() ?? "";
			}
		}
		catch
		{
			// fallthrough
		}

		// If shape changes, return raw body so trace shows it.
		return body;
	}

    private static async Task<string> InvokeOpenAiAsync(AppConfig cfg, string model, string systemPrompt, string userText, CancellationToken ct)
    {
        if (cfg.Providers?.OpenAI?.Enabled != true)
            throw new InvalidOperationException("[digest:error] OpenAI provider is disabled.");

        var baseUrl = (cfg.Providers.OpenAI.BaseUrl ?? "").Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
            throw new InvalidOperationException("[digest:error] OpenAI BaseUrl is empty (Providers.OpenAI.BaseUrl).");

        var apiKey = (cfg.Providers.OpenAI.ApiKey ?? "").Trim();
        if (apiKey.Length == 0)
            throw new InvalidOperationException("[digest:error] OpenAI ApiKey is empty (Providers.OpenAI.ApiKey).");

        var url = baseUrl + "/chat/completions";

        var payload = new
        {
            model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt ?? "" },
                new { role = "user", content = userText ?? "" }
            }
        };

        var reqJson = JsonSerializer.Serialize(payload);

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(reqJson, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(cfg.Providers.OpenAI.Organization))
            req.Headers.Add("OpenAI-Organization", cfg.Providers.OpenAI.Organization);
        if (!string.IsNullOrWhiteSpace(cfg.Providers.OpenAI.Project))
            req.Headers.Add("OpenAI-Project", cfg.Providers.OpenAI.Project);

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"[digest:error] OpenAI chat/completions HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

        try
        {
            using var doc = JsonDocument.Parse(body);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var msg = choices[0].GetProperty("message");
                var content = msg.GetProperty("content").GetString();
                return content ?? "";
            }
        }
        catch { }

        return body;
    }

    private static async Task<string> InvokeHuggingFaceAsync(AppConfig cfg, string model, string systemPrompt, string userText, CancellationToken ct)
    {
        if (cfg.Providers?.HuggingFace?.Enabled != true)
            throw new InvalidOperationException("[digest:error] HuggingFace provider is disabled.");

        var baseUrl = (cfg.Providers.HuggingFace.BaseUrl ?? "").Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
            throw new InvalidOperationException("[digest:error] HuggingFace BaseUrl is empty (Providers.HuggingFace.BaseUrl).");

        var apiKey = (cfg.Providers.HuggingFace.ApiKey ?? "").Trim();
        if (apiKey.Length == 0)
            throw new InvalidOperationException("[digest:error] HuggingFace ApiKey is empty (Providers.HuggingFace.ApiKey).");

        var url = baseUrl + "/models/" + model;

        var combined = (systemPrompt ?? "") + "\n\n" + (userText ?? "");

        var payload = new
        {
            inputs = combined,
            parameters = new { return_full_text = false }
        };

        var reqJson = JsonSerializer.Serialize(payload);

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(reqJson, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"[digest:error] HuggingFace inference HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                if (first.ValueKind == JsonValueKind.Object &&
                    first.TryGetProperty("generated_text", out var gt) &&
                    gt.ValueKind == JsonValueKind.String)
                {
                    return gt.GetString() ?? "";
                }
            }
        }
        catch { }

        return body;
    }
}
