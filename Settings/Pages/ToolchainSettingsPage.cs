#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RahBuilder.Settings;
using RahBuilder.Workflow;
using RahOllamaOnly.Tools;
using RahOllamaOnly.Tools.Prompt;

namespace RahBuilder.Settings.Pages;

public sealed class ToolchainSettingsPage : UserControl
{
    private readonly AppConfig _config;

    private readonly Button _load;
    private readonly Label _status;
    private readonly ListView _list;

    public ToolchainSettingsPage(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Padding = new Padding(6) };
        _load = new Button { Text = "Load + Validate", Width = 120, Height = 30 };
        _status = new Label { AutoSize = true, Padding = new Padding(10, 8, 0, 0) };

        _load.Click += (_, _) => LoadAndRender();

        top.Controls.Add(_load);
        top.Controls.Add(_status);

        _list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
        _list.Columns.Add("ToolId", 220);
        _list.Columns.Add("Prompt-Gated", 100);
        _list.Columns.Add("Desc", 700);

        var footer = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(6),
            Text = "Tools load from repo-relative manifests based on Execution Target. Prompts load from Tools/Prompt (or target-specific prompt folder if present)."
        };

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(_list, 0, 1);
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);

        LoadAndRender();
    }

    private void LoadAndRender()
    {
        _list.Items.Clear();

        var toolsPath = ToolchainResolver.ResolveToolManifestPath(_config);
        var promptsDir = ToolchainResolver.ResolveToolPromptsFolder(_config);
        if (string.IsNullOrWhiteSpace(toolsPath) || !File.Exists(toolsPath))
        {
            _status.Text = $"Missing tools manifest: {toolsPath}";
            return;
        }

        ToolManifest manifest;
        try
        {
            manifest = ToolManifestLoader.LoadFromFile(toolsPath);
        }
        catch (Exception ex)
        {
            _status.Text = $"Failed to load manifest: {ex.Message}";
            return;
        }

        // Collect toolIds from prompt folder, stripping extensions.
        var promptIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(promptsDir))
        {
            foreach (var file in Directory.GetFiles(promptsDir))
            {
                var id = ToolPromptRegistry.NormalizeToolIdFromPromptFile(file);
                if (!string.IsNullOrWhiteSpace(id))
                    promptIds.Add(id);
            }
        }

        int yes = 0, no = 0;

        foreach (var tool in manifest.ToolsById.Values.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase))
        {
            var gated = promptIds.Contains(tool.Id) ? "YES" : "NO";
            if (gated == "YES") yes++; else no++;

            _list.Items.Add(new ListViewItem(new[]
            {
                tool.Id,
                gated,
                tool.Description ?? ""
            }));
        }

        _status.Text = $"tools={manifest.ToolsById.Count} prompts(ids)={promptIds.Count} gated_yes={yes} gated_no={no}";
    }
}
