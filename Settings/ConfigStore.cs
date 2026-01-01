#nullable enable
using System;
using System.IO;
using System.Text.Json;

namespace RahBuilder.Settings;

public static class ConfigStore
{
    private const string FileName = "appsettings.local.json";

    public static AppConfig Load()
    {
        var path = GetConfigPath();

        AppConfig cfg;
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions()) ?? new AppConfig();
            }
            catch
            {
                cfg = new AppConfig();
            }
        }
        else
        {
            cfg = new AppConfig();
        }

        Normalize(cfg);
        return cfg;
    }

    public static void Save(AppConfig cfg)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));

        Normalize(cfg);

        var path = GetConfigPath();
        var json = JsonSerializer.Serialize(cfg, JsonOptions());
        File.WriteAllText(path, json);
    }

    private static void Normalize(AppConfig cfg)
    {
        // Ensure sub-objects exist
        cfg.General ??= new GeneralSettings();
        cfg.Providers ??= new ProvidersSettings();
        cfg.WorkflowGraph ??= new WorkflowGraphSettings();
        cfg.Toolchain ??= new ToolchainSettings();
        cfg.Orchestrator ??= new OrchestratorSettings();

        // Ensure orchestrator has stable required roles
        cfg.Orchestrator.EnsureDefaults();

        // If you have other migration/normalization hooks, call them here.
        // DO NOT reintroduce legacy scalar RolePrompt_* properties.
    }

    private static string GetConfigPath()
    {
        // Keep it simple: config lives beside the executable.
        return Path.Combine(AppContext.BaseDirectory, FileName);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
