#nullable enable
using System;
using System.Windows.Forms;
using RahBuilder.Settings.Pages;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Settings;

public sealed class SettingsHostControl : UserControl
{
    private readonly AppConfig _config;
    private readonly Action _afterSaved;
    private readonly RunTrace? _trace;
    private TabControl? _tabs;
    private ProvidersSettingsPage? _providersPage;

    public SettingsHostControl(AppConfig config, Action afterSaved, RunTrace? trace = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _afterSaved = afterSaved ?? (() => { });
        _trace = trace;

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

        _providersPage = new ProvidersSettingsPage(_config, _trace);

        AddTab(tabs, "General", new GeneralSettingsPage(_config, _trace));
        AddTab(tabs, "Providers", _providersPage);
        AddTab(tabs, "Orchestrator", new OrchestratorSettingsPage(_config));
        AddTab(tabs, "Workflow Graph", new WorkflowGraphSettingsPage(_config));
        AddTab(tabs, "Toolchain", new ToolchainSettingsPage(_config));
        AddTab(tabs, "Tooling Diagnostics", new ToolingDiagnosticsPage(_config));

        Controls.Clear();
        Controls.Add(tabs);
        _tabs = tabs;
    }

    private static void AddTab(TabControl tabs, string title, Control page)
    {
        page.Dock = DockStyle.Fill;
        tabs.TabPages.Add(new TabPage(title) { Controls = { page } });
    }

    public void FocusProvidersTab()
    {
        if (_tabs == null) return;
        for (var i = 0; i < _tabs.TabPages.Count; i++)
        {
            if (string.Equals(_tabs.TabPages[i].Text, "Providers", StringComparison.OrdinalIgnoreCase))
            {
                _tabs.SelectedIndex = i;
                break;
            }
        }

        _providersPage?.FocusProviderToggle();
    }
}
