#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using RahBuilder.Workflow;

namespace RahBuilder.Ui;

public sealed class PlanEditorForm : Form
{
    private readonly BindingList<StepRow> _rows;
    private readonly ToolPlan _original;

    public ToolPlan? Result { get; private set; }

    public PlanEditorForm(ToolPlan plan)
    {
        _original = plan ?? throw new ArgumentNullException(nameof(plan));

        Text = "Modify Plan";
        Width = 700;
        Height = 480;

        _rows = new BindingList<StepRow>(plan.Steps.Select(s => new StepRow
        {
            Id = s.Id,
            ToolId = s.ToolId,
            StoredName = s.Inputs.TryGetValue("storedName", out var n) ? n : "",
            Why = s.Why
        }).ToList());

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = true,
            DataSource = _rows,
            AllowUserToAddRows = true
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6)
        };

        var save = new Button { Text = "Save", AutoSize = true };
        save.Click += (_, _) => SaveAndClose();
        var cancel = new Button { Text = "Cancel", AutoSize = true };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        Controls.Add(grid);
        Controls.Add(buttons);
    }

    private void SaveAndClose()
    {
        var steps = new List<ToolPlanStep>();
        foreach (var row in _rows)
        {
            if (row == null) continue;
            if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.ToolId) || string.IsNullOrWhiteSpace(row.StoredName))
                continue;

            var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedName"] = row.StoredName.Trim()
            };
            steps.Add(new ToolPlanStep(row.Id.Trim(), row.ToolId.Trim(), inputs, row.Why?.Trim() ?? ""));
        }

        if (steps.Count == 0)
        {
            MessageBox.Show(this, "At least one step is required.", "Invalid plan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Result = new ToolPlan(_original.Mode, _original.TweakFirst, steps);
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed class StepRow
    {
        public string Id { get; set; } = "";
        public string ToolId { get; set; } = "";
        public string StoredName { get; set; } = "";
        public string Why { get; set; } = "";
    }
}
