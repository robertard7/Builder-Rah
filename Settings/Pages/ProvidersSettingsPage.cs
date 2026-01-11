#nullable enable
using System;
using System.Windows.Forms;
using RahBuilder.Settings;

namespace RahBuilder.Settings.Pages;

public sealed class ProvidersSettingsPage : UserControl
{
    private readonly AppConfig _config;
    private readonly CheckBox _providerEnabled;
    private readonly ToolTip _providerTip;

    public ProvidersSettingsPage(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _providerTip = new ToolTip();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            AutoScroll = true
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _providerEnabled = new CheckBox
        {
            Text = "Provider Enabled",
            Checked = _config.General.ProviderEnabled,
            AutoSize = true
        };
        _providerEnabled.CheckedChanged += (_, _) =>
        {
            _config.General.ProviderEnabled = _providerEnabled.Checked;
            AutoSave.Touch();
        };

        var providerBox = new GroupBox { Text = "Provider", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var providerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        var desc = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Text = "When off, all language model calls are suppressed; workflow will request you re-enable this before running."
        };
        _providerTip.SetToolTip(_providerEnabled, desc.Text);
        providerPanel.Controls.Add(_providerEnabled);
        providerPanel.Controls.Add(desc);
        providerBox.Controls.Add(providerPanel);

        root.Controls.Add(providerBox, 0, 0);
        root.Controls.Add(BuildOpenAI(), 0, 1);
        root.Controls.Add(BuildHuggingFace(), 0, 2);
        root.Controls.Add(BuildOllama(), 0, 3);

        Controls.Add(root);
    }

    public void FocusProviderToggle()
    {
        if (_providerEnabled.CanFocus)
            _providerEnabled.Focus();
    }

    private Control BuildOpenAI()
    {
        var gb = new GroupBox { Text = "OpenAI", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var grid = NewTwoColGrid();

        int row = 0;
        AddBool(grid, ref row, "Enabled", () => _config.Providers.OpenAI.Enabled, v => _config.Providers.OpenAI.Enabled = v);
        AddText(grid, ref row, "Base URL", () => _config.Providers.OpenAI.BaseUrl, v => _config.Providers.OpenAI.BaseUrl = v);
        AddSecret(grid, ref row, "API Key", () => _config.Providers.OpenAI.ApiKey, v => _config.Providers.OpenAI.ApiKey = v);
        AddText(grid, ref row, "Organization", () => _config.Providers.OpenAI.Organization, v => _config.Providers.OpenAI.Organization = v);
        AddText(grid, ref row, "Project", () => _config.Providers.OpenAI.Project, v => _config.Providers.OpenAI.Project = v);

        gb.Controls.Add(grid);
        return gb;
    }

    private Control BuildHuggingFace()
    {
        var gb = new GroupBox { Text = "HuggingFace", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var grid = NewTwoColGrid();

        int row = 0;
        AddBool(grid, ref row, "Enabled", () => _config.Providers.HuggingFace.Enabled, v => _config.Providers.HuggingFace.Enabled = v);
        AddText(grid, ref row, "Base URL", () => _config.Providers.HuggingFace.BaseUrl, v => _config.Providers.HuggingFace.BaseUrl = v);
        AddSecret(grid, ref row, "API Key", () => _config.Providers.HuggingFace.ApiKey, v => _config.Providers.HuggingFace.ApiKey = v);

        gb.Controls.Add(grid);
        return gb;
    }

    private Control BuildOllama()
    {
        var gb = new GroupBox { Text = "Ollama", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var grid = NewTwoColGrid();

        int row = 0;
        AddBool(grid, ref row, "Enabled", () => _config.Providers.Ollama.Enabled, v => _config.Providers.Ollama.Enabled = v);
        AddText(grid, ref row, "Base URL", () => _config.Providers.Ollama.BaseUrl, v => _config.Providers.Ollama.BaseUrl = v);

        gb.Controls.Add(grid);
        return gb;
    }

    private static TableLayoutPanel NewTwoColGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    private static void AddText(TableLayoutPanel grid, ref int row, string label, Func<string> get, Action<string> set)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };
        var tb = new TextBox { Dock = DockStyle.Fill, Text = get() ?? "" };
        tb.TextChanged += (_, _) => { set(tb.Text); AutoSave.Touch(); };
        grid.Controls.Add(l, 0, row);
        grid.Controls.Add(tb, 1, row);
        row++;
    }

    private static void AddSecret(TableLayoutPanel grid, ref int row, string label, Func<string> get, Action<string> set)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };
        var tb = new TextBox { Dock = DockStyle.Fill, Text = get() ?? "", UseSystemPasswordChar = true };
        tb.TextChanged += (_, _) => { set(tb.Text); AutoSave.Touch(); };
        grid.Controls.Add(l, 0, row);
        grid.Controls.Add(tb, 1, row);
        row++;
    }

    private static void AddBool(TableLayoutPanel grid, ref int row, string label, Func<bool> get, Action<bool> set)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 10, 0) };
        var cb = new CheckBox { Checked = get(), AutoSize = true, Anchor = AnchorStyles.Left };
        cb.CheckedChanged += (_, _) => { set(cb.Checked); AutoSave.Touch(); };
        grid.Controls.Add(l, 0, row);
        grid.Controls.Add(cb, 1, row);
        row++;
    }
}
