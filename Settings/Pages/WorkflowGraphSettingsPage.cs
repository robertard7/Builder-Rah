#nullable enable
using System;
using System.Windows.Forms;
using RahBuilder.Workflow;
using RahBuilder.Settings;

namespace RahBuilder.Settings.Pages;

public sealed class WorkflowGraphSettingsPage : UserControl
{
    private readonly AppConfig _config;
    private readonly TextBox _box;
    private readonly Label _hash;

    public WorkflowGraphSettingsPage(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        _hash = new Label { AutoSize = true, Padding = new Padding(6, 8, 0, 0) };
        top.Controls.Add(_hash);

        _box = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            WordWrap = false,
            AcceptsTab = true,
            Text = _config.WorkflowGraph.MermaidText ?? ""
        };

        _box.TextChanged += (_, _) =>
        {
            _config.WorkflowGraph.MermaidText = _box.Text;
            MermaidGraphHub.Publish(_box.Text);     // immediate publish (graph is truth)
            AutoSave.Touch();                        // debounced persist
            UpdateHashLabel();
        };

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(_box, 0, 1);

        Controls.Add(root);

        MermaidGraphHub.Publish(_box.Text);
        UpdateHashLabel();
    }

    private void UpdateHashLabel()
    {
        _hash.Text = $"GraphHash: {MermaidGraphHub.CurrentHash}";
    }
}
