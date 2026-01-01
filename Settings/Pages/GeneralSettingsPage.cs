#nullable enable
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RahBuilder.Workflow;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Settings.Pages;

public sealed class GeneralSettingsPage : UserControl
{
    private readonly AppConfig _config;
    private readonly RunTrace? _trace;

    public GeneralSettingsPage(AppConfig config, RunTrace? trace = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _trace = trace;

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

        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var repoLabel = new Label { Text = "Repo Root", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };
        var repoPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var repoBox = new TextBox { Width = 420, Text = _config.General.RepoRoot ?? "" };
        repoBox.TextChanged += (_, _) => { _config.General.RepoRoot = repoBox.Text; AutoSave.Touch(); };
        var browseBtn = new Button { Text = "Browse…", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        browseBtn.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Select Repo Root" };
            try
            {
                var current = _config.General.RepoRoot ?? "";
                if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                    dlg.SelectedPath = current;
            }
            catch { }

            if (dlg.ShowDialog() == DialogResult.OK)
                repoBox.Text = dlg.SelectedPath;
        };
        var openRepoBtn = new Button { Text = "Open Folder", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        openRepoBtn.Click += (_, _) =>
        {
            var path = (_config.General.RepoRoot ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch { }
        };
        var validateBtn = new Button { Text = "Validate RepoRoot", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        validateBtn.Click += (_, _) =>
        {
            var scope = RepoScope.Resolve(_config);
            var message = scope.Message;
            if (!string.IsNullOrWhiteSpace(scope.GitTopLevel))
                message += $" (GitTopLevel={scope.GitTopLevel})";
            if (_trace != null)
                _trace.Emit(message);
            else
                Debug.WriteLine(message);

            MessageBox.Show(message, "RepoRoot", MessageBoxButtons.OK, scope.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        };
        repoPanel.Controls.Add(repoBox);
        repoPanel.Controls.Add(browseBtn);
        repoPanel.Controls.Add(openRepoBtn);
        repoPanel.Controls.Add(validateBtn);
        grid.Controls.Add(repoLabel, 0, row);
        grid.Controls.Add(repoPanel, 1, row);
        row++;

        AddRow("Sandbox Host Path", () => _config.General.SandboxHostPath, v => _config.General.SandboxHostPath = v);
        AddRow("Sandbox Container Path", () => _config.General.SandboxContainerPath, v => _config.General.SandboxContainerPath = v);

        AddBool("TweakFirst mode (avoid rebuilds)", () => _config.General.TweakFirstMode, v => _config.General.TweakFirstMode = v);
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var convoLabel = new Label { Text = "Conversation Mode", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };
        var convoCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left };
        convoCombo.Items.AddRange(Enum.GetNames(typeof(ConversationMode)));
        convoCombo.SelectedItem = _config.General.ConversationMode.ToString();
        convoCombo.SelectedIndexChanged += (_, _) =>
        {
            if (Enum.TryParse<ConversationMode>(convoCombo.SelectedItem?.ToString(), out var mode))
            {
                _config.General.ConversationMode = mode;
                AutoSave.Touch();
            }
        };
        grid.Controls.Add(convoLabel, 0, row);
        grid.Controls.Add(convoCombo, 1, row);
        row++;
        AddRow("Tools Manifest Path (tools.json)", () => _config.General.ToolsPath, v => _config.General.ToolsPath = v);
        AddRow("Tool Prompts Folder (Tools/Prompt)", () => _config.General.ToolPromptsPath, v => _config.General.ToolPromptsPath = v);
        AddRow("BlueprintTemplates Folder", () => _config.General.BlueprintTemplatesPath, v => _config.General.BlueprintTemplatesPath = v);

        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var inboxLabel = new Label { Text = "Attachments Inbox (host path)", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };
        var inboxPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var inboxBox = new TextBox { Width = 420, Text = _config.General.InboxHostPath ?? "" };
        inboxBox.TextChanged += (_, _) => { _config.General.InboxHostPath = inboxBox.Text; AutoSave.Touch(); };
        var inboxBtn = new Button { Text = "Open Folder", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        inboxBtn.Click += (_, _) =>
        {
            try
            {
                var path = _config.General.InboxHostPath ?? "";
                if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RahBuilder", "inbox");
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch { }
        };
        inboxPanel.Controls.Add(inboxBox);
        inboxPanel.Controls.Add(inboxBtn);
        grid.Controls.Add(inboxLabel, 0, row);
        grid.Controls.Add(inboxPanel, 1, row);
        row++;

        AddRow("Accepted attachment extensions (comma-separated)", () => _config.General.AcceptedAttachmentExtensions, v => _config.General.AcceptedAttachmentExtensions = v);
        AddRow("Max attachment bytes", () => _config.General.MaxAttachmentBytes.ToString(), v =>
        {
            if (long.TryParse(v, out var parsed) && parsed > 0)
            {
                _config.General.MaxAttachmentBytes = parsed;
                AutoSave.Touch();
            }
        });
        AddRow("Max total inbox bytes", () => _config.General.MaxTotalInboxBytes.ToString(), v =>
        {
            if (long.TryParse(v, out var parsed) && parsed > 0)
            {
                _config.General.MaxTotalInboxBytes = parsed;
                AutoSave.Touch();
            }
        });

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

        AddMultiline(
            "Tool Plan Prompt (tool_plan.v1 only)",
            () => _config.General.ToolPlanPrompt,
            v => _config.General.ToolPlanPrompt = v,
            height: 200
        );

        AddMultiline(
            "Final Answer Prompt",
            () => _config.General.FinalAnswerPrompt,
            v => _config.General.FinalAnswerPrompt = v,
            height: 160
        );

        Controls.Add(grid);
    }
}
