#nullable enable
using System;
using System.Windows.Forms;
using RahBuilder.Settings.Pages;

namespace RahBuilder.Settings;

public sealed class SettingsHostControl : UserControl
{
    private readonly AppConfig _config;
    private readonly Action _afterSaved;

    public SettingsHostControl(AppConfig config, Action afterSaved)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _afterSaved = afterSaved ?? (() => { });

        Dock = DockStyle.Fill;

        AutoSave.Wire(this, () =>
        {
            ConfigStore.Save(_config);
            _afterSaved();
        });

        BuildUi();
    }

    private void BuildUi()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        AddTab(tabs, "General", new GeneralSettingsPage(_config));
        AddTab(tabs, "Providers", new ProvidersSettingsPage(_config));
        AddTab(tabs, "Orchestrator", new OrchestratorSettingsPage(_config));
        AddTab(tabs, "Workflow Graph", new WorkflowGraphSettingsPage(_config));
        AddTab(tabs, "Toolchain", new ToolchainSettingsPage(_config));
        AddTab(tabs, "Tooling Diagnostics", new ToolingDiagnosticsPage(_config));

        Controls.Clear();
        Controls.Add(tabs);
    }

    private static void AddTab(TabControl tabs, string title, Control page)
    {
        page.Dock = DockStyle.Fill;
        tabs.TabPages.Add(new TabPage(title) { Controls = { page } });
    }
}
