// Settings/Pages/OrchestratorSettingsPage.cs
#nullable enable
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RahBuilder.Settings;
using RahBuilder.Ui;

namespace RahBuilder.Settings.Pages;

public sealed class OrchestratorSettingsPage : UserControl, ISettingsPageProvider
{
    public new string Name => "Orchestrator";

    private readonly AppConfig _config;

    private bool _uiReady;
    private bool _binding;

    private TextBox _mode = null!;
    private CheckBox _openAi = null!;
    private CheckBox _hf = null!;
    private CheckBox _ollama = null!;

    private DataGridView _grid = null!;
    private TextBox _rolePurpose = null!;
    private TextBox _promptText = null!;
    private Label _status = null!;

    public OrchestratorSettingsPage(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Dock = DockStyle.Fill;

        _config.Orchestrator ??= new OrchestratorSettings();
        _config.Orchestrator.EnsureDefaults();

        BuildUi();
        BindFromConfig();
    }

    public Control BuildPage(AppConfig config) => new OrchestratorSettingsPage(config);

    private void BuildUi()
    {
        _uiReady = false;
        _binding = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(10),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        // ===== Top controls
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 3
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        top.Controls.Add(new Label
        {
            Text = "Mode (single|multi)",
            AutoSize = true,
            Margin = new Padding(0, 6, 10, 6)
        }, 0, 0);

        _mode = new TextBox { Dock = DockStyle.Fill };
        _mode.TextChanged += (_, __) =>
        {
            if (!_uiReady || _binding) return;
            _config.Orchestrator.Mode = (_mode.Text ?? "").Trim();
            AutoSave.Touch("orchestrator.mode");
        };
        top.Controls.Add(_mode, 1, 0);
        top.SetColumnSpan(_mode, 3);

        _openAi = new CheckBox { Text = "Enable OpenAI pool", AutoSize = true };
        _openAi.CheckedChanged += (_, __) =>
        {
            if (!_uiReady || _binding) return;
            _config.Orchestrator.EnableOpenAiPool = _openAi.Checked;
            AutoSave.Touch("orchestrator.pool.openai");
        };

        _hf = new CheckBox { Text = "Enable HuggingFace pool", AutoSize = true };
        _hf.CheckedChanged += (_, __) =>
        {
            if (!_uiReady || _binding) return;
            _config.Orchestrator.EnableHuggingFacePool = _hf.Checked;
            AutoSave.Touch("orchestrator.pool.hf");
        };

        _ollama = new CheckBox { Text = "Enable Ollama pool", AutoSize = true };
        _ollama.CheckedChanged += (_, __) =>
        {
            if (!_uiReady || _binding) return;
            _config.Orchestrator.EnableOllamaPool = _ollama.Checked;
            AutoSave.Touch("orchestrator.pool.ollama");
        };

        top.Controls.Add(_openAi, 0, 1);
        top.Controls.Add(_hf, 1, 1);
        top.Controls.Add(_ollama, 2, 1);

        _status = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 0),
            Text = ""
        };
        top.Controls.Add(_status, 0, 2);
        top.SetColumnSpan(_status, 4);

        root.Controls.Add(top, 0, 0);
        root.SetColumnSpan(top, 2);

        // ===== Grid (left)
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Role", FillWeight = 18, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Provider", FillWeight = 18, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Model", FillWeight = 24, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Prompt (inline preview)", FillWeight = 40, ReadOnly = true });

        _grid.SelectionChanged += (_, __) =>
        {
            if (!_uiReady || _binding) return;
            SyncEditors();
        };

        root.Controls.Add(_grid, 0, 1);
        root.SetRowSpan(_grid, 5);

        // ===== Right side editors
        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(8, 0, 0, 0)
        };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        right.Controls.Add(new Label
        {
            Text = "Role Purpose (free text)",
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 4)
        }, 0, 0);

        _rolePurpose = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };
        _rolePurpose.TextChanged += (_, __) =>
        {
            if (!_uiReady || _binding) return;
            var role = SelectedRole();
            if (role == null) return;

            role.RolePurpose = _rolePurpose.Text ?? "";
            AutoSave.Touch($"orchestrator.rolePurpose.{role.Role}");
        };
        right.Controls.Add(_rolePurpose, 0, 1);

        right.Controls.Add(new Label
        {
            Text = "Prompt (inline). Paste/type the full prompt here.",
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 4)
        }, 0, 2);

        _promptText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };
        _promptText.TextChanged += (_, __) =>
        {
            if (!_uiReady || _binding) return;
            var role = SelectedRole();
            if (role == null) return;

            role.PromptText = _promptText.Text ?? "";
            AutoSave.Touch($"orchestrator.promptText.{role.Role}");

            // Keep selection and avoid loop: refresh preview only.
            RefreshGrid(keepSelection: true);
        };
        right.Controls.Add(_promptText, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false
        };

        var add = new Button { Text = "Add Role", AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
        add.Click += (_, __) =>
        {
            if (!_uiReady || _binding) return;

            _config.Orchestrator.Roles.Add(new OrchestratorRoleConfig
            {
                Role = $"role{_config.Orchestrator.Roles.Count + 1}",
                Provider = "Ollama",
                Model = "",
                PromptText = "",
                RolePurpose = "",
                PromptId = "default"
            });

            AutoSave.Touch("orchestrator.role.add");
            RefreshGrid(selectLast: true);
            SyncEditors();
        };

        var del = new Button { Text = "Delete Role", AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
        del.Click += (_, __) =>
        {
            if (!_uiReady || _binding) return;

            var role = SelectedRole();
            if (role == null) return;

            _config.Orchestrator.Roles.Remove(role);
            AutoSave.Touch("orchestrator.role.delete");
            RefreshGrid(selectFirst: true);
            SyncEditors();
        };

        var edit = new Button { Text = "Edit Provider/Model…", AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
        edit.Click += (_, __) =>
        {
            if (!_uiReady || _binding) return;
            EditSelectedRole();
        };

        buttons.Controls.Add(add);
        buttons.Controls.Add(del);
        buttons.Controls.Add(edit);

        right.Controls.Add(buttons, 0, 4);

        right.Controls.Add(new Label
        {
            Text =
                "Roles include: chat/router/planner/executor/reviewer/embed/vision.\n" +
                "Set provider+model per role. Vision is for image describe/caption tools.\n" +
                "PromptId still exists internally for legacy blueprint routing, but UI uses PromptText now.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 0)
        }, 0, 5);

        root.Controls.Add(right, 1, 1);
        root.SetRowSpan(right, 5);

        Controls.Add(root);

        _binding = false;
        _uiReady = true;
    }

    private void BindFromConfig()
    {
        _binding = true;
        try
        {
            _mode.Text = _config.Orchestrator.Mode ?? "single";
            _openAi.Checked = _config.Orchestrator.EnableOpenAiPool;
            _hf.Checked = _config.Orchestrator.EnableHuggingFacePool;
            _ollama.Checked = _config.Orchestrator.EnableOllamaPool;

            RefreshGrid(selectFirst: true);
            SyncEditors();
        }
        finally
        {
            _binding = false;
        }
    }

    private void RefreshGrid(bool selectFirst = false, bool selectLast = false, bool keepSelection = false)
    {
        // Always safe: this can be called from TextChanged handlers.
        var keepRole = (string?)null;
        if (keepSelection && _grid.SelectedRows.Count == 1)
            keepRole = _grid.SelectedRows[0].Cells[0].Value?.ToString();

        _binding = true;
        try
        {
            _grid.Rows.Clear();

            foreach (var r in _config.Orchestrator.Roles)
            {
                var preview = (r.PromptText ?? "").Trim();
                if (preview.Length > 60) preview = preview[..60] + "…";
                if (string.IsNullOrWhiteSpace(preview)) preview = "(empty)";

                _grid.Rows.Add(r.Role, r.Provider, r.Model, preview);
            }

            if (_grid.Rows.Count == 0)
            {
                UpdateStatus(null);
                return;
            }

            if (!string.IsNullOrWhiteSpace(keepRole))
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (string.Equals(row.Cells[0].Value?.ToString(), keepRole, StringComparison.OrdinalIgnoreCase))
                    {
                        row.Selected = true;
                        UpdateStatus(keepRole);
                        return;
                    }
                }
            }

            if (selectLast) _grid.Rows[^1].Selected = true;
            else if (selectFirst) _grid.Rows[0].Selected = true;
            else if (_grid.SelectedRows.Count == 0) _grid.Rows[0].Selected = true;

            UpdateStatus(SelectedRole()?.Role);
        }
        finally
        {
            _binding = false;
        }
    }

    private void UpdateStatus(string? selectedRole)
    {
        var count = _config.Orchestrator.Roles?.Count ?? 0;
        _status.Text = selectedRole == null
            ? $"Roles: {count} (no selection)"
            : $"Roles: {count} | Selected: {selectedRole}";
    }

    private OrchestratorRoleConfig? SelectedRole()
    {
        if (_grid.SelectedRows.Count != 1) return null;
        var roleName = _grid.SelectedRows[0].Cells[0].Value?.ToString() ?? "";
        return _config.Orchestrator.Roles.FirstOrDefault(r =>
            string.Equals(r.Role, roleName, StringComparison.OrdinalIgnoreCase));
    }

    private void SyncEditors()
    {
        var role = SelectedRole();
        if (role == null)
        {
            _binding = true;
            try
            {
                _rolePurpose.Enabled = false;
                _promptText.Enabled = false;
                _rolePurpose.Text = "";
                _promptText.Text = "";
                UpdateStatus(null);
            }
            finally
            {
                _binding = false;
            }
            return;
        }

        _binding = true;
        try
        {
            _rolePurpose.Enabled = true;
            _promptText.Enabled = true;

            var rp = role.RolePurpose ?? "";
            if (!string.Equals(_rolePurpose.Text, rp, StringComparison.Ordinal))
                _rolePurpose.Text = rp;

            var pt = role.PromptText ?? "";
            if (!string.Equals(_promptText.Text, pt, StringComparison.Ordinal))
                _promptText.Text = pt;

            UpdateStatus(role.Role);
        }
        finally
        {
            _binding = false;
        }
    }

    private void EditSelectedRole()
    {
        var role = SelectedRole();
        if (role == null) return;

        using var dlg = new Form
        {
            Text = $"Edit Role: {role.Role}",
            StartPosition = FormStartPosition.CenterParent,
            Width = 720,
            Height = 240,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var provider = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        provider.Items.AddRange(new object[] { "OpenAI", "HuggingFace", "Ollama" });
        provider.SelectedItem = string.IsNullOrWhiteSpace(role.Provider) ? "Ollama" : role.Provider;

        var model = new TextBox { Dock = DockStyle.Fill, Text = role.Model ?? "" };

        root.Controls.Add(new Label { Text = "Provider", AutoSize = true, Margin = new Padding(0, 6, 10, 6) }, 0, 0);
        root.Controls.Add(provider, 1, 0);

        root.Controls.Add(new Label { Text = "Model", AutoSize = true, Margin = new Padding(0, 6, 10, 6) }, 0, 1);
        root.Controls.Add(model, 1, 1);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(12, 6, 12, 6) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(12, 6, 12, 6) };

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        btnRow.Controls.Add(ok);
        btnRow.Controls.Add(cancel);

        root.Controls.Add(btnRow, 0, 3);
        root.SetColumnSpan(btnRow, 2);

        dlg.Controls.Add(root);

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        role.Provider = provider.SelectedItem?.ToString() ?? role.Provider;
        role.Model = (model.Text ?? "").Trim();

        AutoSave.Touch("orchestrator.role.edit");
        RefreshGrid(keepSelection: true);
        SyncEditors();
    }
}
