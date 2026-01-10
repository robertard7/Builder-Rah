#nullable enable
using System;
using System.Linq;
using System.Windows.Forms;
using RahOllamaOnly.Tools.Diagnostics;

namespace RahBuilder.Settings.Pages;

public sealed class ToolingDiagnosticsPage : UserControl, ISettingsPageProvider
{
    public new string Name => "Tooling Diagnostics";

    private readonly Label _status;

    public ToolingDiagnosticsPage(AppConfig config)
    {
        Dock = DockStyle.Fill;

        _status = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = "Loading diagnostics…"
        };

        Controls.Add(_status);
        UpdateStatus(ToolingDiagnosticsHub.Latest);
        ToolingDiagnosticsHub.Updated += UpdateStatus;
    }

    private void UpdateStatus(ToolingDiagnostics diag)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateStatus(diag)));
            return;
        }

        var selected = diag.SelectedBlueprints.Any() ? string.Join(", ", diag.SelectedBlueprints) : "(none)";
        var breakdown = diag.BlueprintTagBreakdown != null && diag.BlueprintTagBreakdown.Any()
            ? string.Join(", ", diag.BlueprintTagBreakdown.Select(kv => $"{kv.Key}:{kv.Value}"))
            : "(none)";
        _status.Text =
            $"Tools: {diag.ActiveToolCount}/{diag.ToolCount} active (prompts={diag.PromptCount}, missing={diag.MissingPrompts.Count}){Environment.NewLine}" +
            $"Toolchain: {diag.State}{Environment.NewLine}" +
            $"Blueprints: {diag.BlueprintSelectable}/{diag.BlueprintTotal} selectable{Environment.NewLine}" +
            $"Selected Blueprints: {selected}{Environment.NewLine}" +
            $"Selection breakdown: {breakdown}";
    }

    public Control BuildPage(AppConfig config) => new ToolingDiagnosticsPage(config);
}
