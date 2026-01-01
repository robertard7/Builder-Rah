#nullable enable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RahBuilder.Settings.Pages;

public sealed class GeneralSettingsPage : UserControl
{
    private readonly AppConfig _config;

    public GeneralSettingsPage(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
            Padding = new Padding(8)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        void AddRow(string label, Func<string> get, Action<string> set)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };
            var tb = new TextBox { Dock = DockStyle.Fill, Text = get() ?? "" };
            tb.TextChanged += (_, _) => { set(tb.Text); AutoSave.Touch(); };
            grid.Controls.Add(l, 0, row);
            grid.Controls.Add(tb, 1, row);
            row++;
        }

        void AddBool(string label, Func<bool> get, Action<bool> set)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };
            var cb = new CheckBox { Checked = get(), AutoSize = true, Anchor = AnchorStyles.Left };
            cb.CheckedChanged += (_, _) => { set(cb.Checked); AutoSave.Touch(); };
            grid.Controls.Add(l, 0, row);
            grid.Controls.Add(cb, 1, row);
            row++;
        }

        void AddMultiline(string label, Func<string> get, Action<string> set, int height = 180)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };

            var tb = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                Font = new Font("Consolas", 9f),
                Height = height,
                Text = get() ?? ""
            };

            tb.TextChanged += (_, _) => { set(tb.Text); AutoSave.Touch(); };

            grid.Controls.Add(l, 0, row);
            grid.Controls.Add(tb, 1, row);
            row++;
        }

        AddRow("Repo Root", () => _config.General.RepoRoot, v => _config.General.RepoRoot = v);
        AddRow("Sandbox Host Path", () => _config.General.SandboxHostPath, v => _config.General.SandboxHostPath = v);
        AddRow("Sandbox Container Path", () => _config.General.SandboxContainerPath, v => _config.General.SandboxContainerPath = v);

        AddRow("Tools Manifest Path (tools.json)", () => _config.General.ToolsPath, v => _config.General.ToolsPath = v);
        AddRow("Tool Prompts Folder (Tools/Prompt)", () => _config.General.ToolPromptsPath, v => _config.General.ToolPromptsPath = v);
        AddRow("BlueprintTemplates Folder", () => _config.General.BlueprintTemplatesPath, v => _config.General.BlueprintTemplatesPath = v);

        AddBool("GraphDriven routing enabled", () => _config.General.GraphDriven, v => _config.General.GraphDriven = v);
        AddBool("Container-only execution (no host tools)", () => _config.General.ContainerOnly, v => _config.General.ContainerOnly = v);
        AddBool("Global clipboard shortcuts + context menus", () => _config.General.EnableGlobalClipboardShortcuts, v => _config.General.EnableGlobalClipboardShortcuts = v);

        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var execLabel = new Label { Text = "Execution Target (WinForms requires WindowsHost)", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };
        var execCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left };
        execCombo.Items.AddRange(new object[] { "WindowsHost", "LinuxContainer" });
        var currentTarget = (_config.General.ExecutionTarget ?? "").Trim();
        if (string.IsNullOrWhiteSpace(currentTarget))
            currentTarget = OperatingSystem.IsWindows() ? "WindowsHost" : "LinuxContainer";
        execCombo.SelectedItem = currentTarget;
        execCombo.SelectedIndexChanged += (_, _) =>
        {
            var value = execCombo.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(value)) return;
            _config.General.ExecutionTarget = value;
            AutoSave.Touch();
        };
        grid.Controls.Add(execLabel, 0, row);
        grid.Controls.Add(execCombo, 1, row);
        row++;

        AddMultiline(
            "Global Job Spec Digest Prompt (JSON-only, no tools)",
            () => _config.General.JobSpecDigestPrompt,
            v => _config.General.JobSpecDigestPrompt = v,
            height: 220
        );

        Controls.Add(grid);
    }
}
