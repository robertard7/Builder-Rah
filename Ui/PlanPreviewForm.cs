#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using RahBuilder.Workflow;

namespace RahBuilder.Ui;

public sealed class PlanPreviewForm : Form
{
    private readonly BindingSource _binding = new();
    private readonly DataGridView _grid;
    private readonly Button _confirm;
    private readonly Button _cancel;
    private readonly Button _modify;

    public PlanDefinition? Result { get; private set; }

    private readonly string _session;

    public PlanPreviewForm(PlanDefinition plan)
    {
        Text = "Plan Preview";
        Width = 720;
        Height = 480;

        _session = plan.Session;
        _binding.DataSource = new List<PlanStep>(plan.Steps);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = true,
            DataSource = _binding,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        _confirm = new Button { Text = "Confirm", AutoSize = true };
        _cancel = new Button { Text = "Cancel", AutoSize = true };
        _modify = new Button { Text = "Modify", AutoSize = true };

        _confirm.Click += (_, _) => Confirm();
        _cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _modify.Click += (_, _) => ModifySelection();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
            AutoSize = true
        };
        buttons.Controls.AddRange(new Control[] { _confirm, _modify, _cancel });

        Controls.Add(_grid);
        Controls.Add(buttons);
    }

    private void Confirm()
    {
        var steps = new List<PlanStep>();
        foreach (PlanStep row in _binding.List)
        {
            if (row == null) continue;
            steps.Add(new PlanStep
            {
                Order = steps.Count + 1,
                ToolId = row.ToolId,
                Description = row.Description,
                Why = row.Why,
                Inputs = row.Inputs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });
        }

        Result = new PlanDefinition
        {
            Session = _session,
            Steps = steps
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ModifySelection()
    {
        if (_grid.SelectedRows.Count == 0)
            return;

        var row = _grid.SelectedRows[0].DataBoundItem as PlanStep;
        if (row == null) return;

        var index = _binding.List.IndexOf(row);
        if (index <= 0) return;

        _binding.Remove(row);
        _binding.Insert(index - 1, row);
        _grid.ClearSelection();
        _grid.Rows[index - 1].Selected = true;
    }
}
