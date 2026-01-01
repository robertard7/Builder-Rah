#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RahBuilder.Settings;

public static class ModelCatalog
{
    public static readonly string[] DefaultOpenAiModels =
    {
        "gpt-4o-mini",
        "gpt-4.1-mini",
        "gpt-4.1",
        "o4-mini"
    };

    public static readonly string[] DefaultHuggingFaceModels =
    {
        "Qwen/Qwen2.5-7B-Instruct",
        "meta-llama/Llama-3.1-8B-Instruct",
        "mistralai/Mistral-7B-Instruct-v0.3"
    };

    public static readonly string[] DefaultOllamaModels =
    {
        "qwen2.5:7b-instruct",
        "qwen2.5:14b-instruct",
        "mistral:latest",
        "llama3.1:8b",
        "phi3:mini",
        "nomic-embed-text:latest"
    };

    public static IReadOnlyList<string> GetModelsForProvider(AppConfig cfg, string provider)
        => provider switch
        {
            "OpenAI" => DefaultOpenAiModels,
            "HuggingFace" => DefaultHuggingFaceModels,
            "Ollama" => DefaultOllamaModels,
            _ => Array.Empty<string>()
        };

    // Role prompts come from BlueprintTemplates (your prompt library), not tool prompts.
    // Minimal: list BlueprintTemplates\manifest.json entries if present, else return "default".
    public static IReadOnlyList<string> GetRolePromptSelectors(AppConfig cfg)
    {
        try
        {
            var dir = (cfg.General.BlueprintTemplatesPath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return new[] { "default" };

            var manifest = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifest))
                return new[] { "default" };

            var json = File.ReadAllText(manifest);
            // ultra-minimal: just surface some stable tokens without binding to a schema yet
            // (we’ll wire a real Blueprint manifest reader next).
            if (json.Contains("\"atoms\"") || json.Contains("\"recipes\""))
                return new[] { "default", "atoms", "recipes", "packs", "graphs" };

            return new[] { "default" };
        }
        catch
        {
            return new[] { "default" };
        }
    }
}
