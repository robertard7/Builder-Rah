#nullable enable
using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RahBuilder.Settings;

public static class ConfigStore
{
    private const string BaseFileName = "appsettings.json";
    private const string LocalFileName = "appsettings.local.json";

    public static AppConfig Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(BaseFileName, optional: true, reloadOnChange: false)
            .AddJsonFile(LocalFileName, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        TryAddUserSecrets(builder);

        AppConfig cfg;
        try
        {
            var configuration = builder.Build();
            cfg = configuration.Get<AppConfig>() ?? new AppConfig();
        }
        catch
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

        var path = GetLocalConfigPath();
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
        cfg.General.Normalize();

        // If you have other migration/normalization hooks, call them here.
        // DO NOT reintroduce legacy scalar RolePrompt_* properties.
    }

    private static void TryAddUserSecrets(ConfigurationBuilder builder)
    {
#if DEBUG
        try
        {
            builder.AddUserSecrets<ConfigStore>(optional: true);
        }
        catch
        {
        }
#endif
    }

    private static string GetLocalConfigPath()
    {
        // Keep it simple: config lives beside the executable.
        return Path.Combine(AppContext.BaseDirectory, LocalFileName);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
