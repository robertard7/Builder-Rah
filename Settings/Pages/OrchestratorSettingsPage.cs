#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RahBuilder.Settings;
using RahBuilder.Tools.BlueprintTemplates;
using RahBuilder.Ui;

namespace RahBuilder.Settings.Pages;

public sealed class OrchestratorSettingsPage : UserControl, ISettingsPageProvider
{
    public new string Name => "Orchestrator";

    private readonly AppConfig _config;

    private TextBox _mode = null!;
    private CheckBox _openAi = null!;
    private CheckBox _hf = null!;
    private CheckBox _ollama = null!;
    private DataGridView _grid = null!;
    private Label _catalogStatus = null!;

    private TextBox _rolePurpose = null!;
    private Label _rolePurposeLabel = null!;

    private IReadOnlyList<BlueprintCatalogEntry> _catalogAll = Array.Empty<BlueprintCatalogEntry>();
    private IReadOnlyList<BlueprintCatalogEntry> _catalogPublic = Array.Empty<BlueprintCatalogEntry>();

    private sealed class PromptItem
    {
        public string Id { get; init; } = "";
        public string Display { get; init; } = "";
        public string Description { get; init; } = "";
        public override string ToString() => Display;
    }

    public OrchestratorSettingsPage(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Dock = DockStyle.Fill;

        _config.Orchestrator ??= new OrchestratorSettings();
        _config.Orchestrator.EnsureDefaults();

        LoadCatalog();
        BuildUi();
        BindFromConfig();
    }

    public Control BuildPage(AppConfig config) => new OrchestratorSettingsPage(config);

    private void LoadCatalog()
    {
        try
        {
            var folder = _config.General?.BlueprintTemplatesPath ?? "";
            _catalogAll = BlueprintCatalog.Load(folder);
            _catalogPublic = BlueprintCatalog.PublicOnly(_catalogAll);
        }
        catch
        {
            _catalogAll = Array.Empty<BlueprintCatalogEntry>();
            _catalogPublic = Array.Empty<BlueprintCatalogEntry>();
        }
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(10),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

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

        top.Controls.Add(new Label { Text = "Mode (single|multi)", AutoSize = true, Margin = new Padding(0, 6, 10, 6) }, 0, 0);

        _mode = new TextBox { Dock = DockStyle.Fill };
        _mode.TextChanged += (_, __) =>
        {
            _config.Orchestrator.Mode = (_mode.Text ?? "").Trim();
            AutoSave.Touch("orchestrator.mode");
        };
        top.Controls.Add(_mode, 1, 0);
        top.SetColumnSpan(_mode, 3);

        _openAi = new CheckBox { Text = "Enable OpenAI pool", AutoSize = true };
        _openAi.CheckedChanged += (_, __) =>
        {
            _config.Orchestrator.EnableOpenAiPool = _openAi.Checked;
            AutoSave.Touch("orchestrator.pool.openai");
        };

        _hf = new CheckBox { Text = "Enable HuggingFace pool", AutoSize = true };
        _hf.CheckedChanged += (_, __) =>
        {
            _config.Orchestrator.EnableHuggingFacePool = _hf.Checked;
            AutoSave.Touch("orchestrator.pool.hf");
        };

        _ollama = new CheckBox { Text = "Enable Ollama pool", AutoSize = true };
        _ollama.CheckedChanged += (_, __) =>
        {
            _config.Orchestrator.EnableOllamaPool = _ollama.Checked;
            AutoSave.Touch("orchestrator.pool.ollama");
        };

        top.Controls.Add(_openAi, 0, 1);
        top.Controls.Add(_hf, 1, 1);
        top.Controls.Add(_ollama, 2, 1);

        _catalogStatus = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 10, 6)
        };

        top.Controls.Add(_catalogStatus, 0, 2);
        top.SetColumnSpan(_catalogStatus, 4);

        root.Controls.Add(top, 0, 0);
        root.SetColumnSpan(top, 2);

        // ===== Grid
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Role", FillWeight = 18, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Provider", FillWeight = 18, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Model", FillWeight = 32, ReadOnly = true });

        var promptCol = new DataGridViewComboBoxColumn
        {
            HeaderText = "Prompt (BlueprintTemplates)",
            FillWeight = 32,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            FlatStyle = FlatStyle.Flat
        };
        _grid.Columns.Add(promptCol);

        _grid.CurrentCellDirtyStateChanged += (_, __) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 3) return;
            ApplyPromptChangeFromGrid(e.RowIndex);
        };

        _grid.DataError += (_, __) =>
        {
            // swallow ComboBox invalid-value popups
        };

        _grid.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 3) return;
            var id = _grid.Rows[e.RowIndex].Cells[3].Value?.ToString() ?? "";
            e.ToolTipText = FindDescription(id);
        };

        _grid.SelectionChanged += (_, __) => SyncRolePurposeEditor();

        root.Controls.Add(_grid, 0, 1);
        root.SetColumnSpan(_grid, 2);

        // ===== Buttons
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false
        };

        var editRole = new Button { Text = "Edit Role…", AutoSize = true, Padding = new Padding(12, 6, 12, 6) };
        editRole.Click += (_, __) => EditSelectedRole();

        var editPrompt = new Button { Text = "Prompt…", AutoSize = true, Padding = new Padding(12, 6, 12, 6) };
        editPrompt.Click += (_, __) => EditSelectedPrompt();

        buttons.Controls.Add(editRole);
        buttons.Controls.Add(editPrompt);

        root.Controls.Add(buttons, 0, 2);
        root.SetColumnSpan(buttons, 2);

        // ===== Role Purpose editor (this is the missing thing you’re yelling about)
        _rolePurposeLabel = new Label
        {
            Text = "Role Purpose (free text). This is what the role does and how it should interpret user intent.",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 4)
        };

        _rolePurpose = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 110
        };

        _rolePurpose.TextChanged += (_, __) =>
        {
            var role = SelectedRole();
            if (role == null) return;

            role.RolePurpose = _rolePurpose.Text ?? "";
            AutoSave.Touch($"orchestrator.rolePurpose.{role.Role}");
        };

        root.Controls.Add(_rolePurposeLabel, 0, 3);
        root.SetColumnSpan(_rolePurposeLabel, 2);

        root.Controls.Add(_rolePurpose, 0, 4);
        root.SetColumnSpan(_rolePurpose, 2);

        Controls.Add(root);
    }

    private void BindFromConfig()
    {
        _mode.Text = _config.Orchestrator.Mode ?? "single";
        _openAi.Checked = _config.Orchestrator.EnableOpenAiPool;
        _hf.Checked = _config.Orchestrator.EnableHuggingFacePool;
        _ollama.Checked = _config.Orchestrator.EnableOllamaPool;

        RefreshCatalogStatus();
        RefreshGrid();
        SyncRolePurposeEditor();
    }

    private void RefreshCatalogStatus()
    {
        var folder = _config.General?.BlueprintTemplatesPath ?? "";
        if (string.IsNullOrWhiteSpace(folder))
        {
            _catalogStatus.Text = "BlueprintTemplates Folder is empty. Set it in Settings → General.";
            return;
        }

        if (_catalogAll.Count == 0)
        {
            _catalogStatus.Text = "No BlueprintTemplates loaded. Check BlueprintTemplates Folder + manifest.json.";
            return;
        }

        _catalogStatus.Text = $"BlueprintTemplates loaded: public={_catalogPublic.Count}, total={_catalogAll.Count}";
    }

    private List<PromptItem> BuildPromptItemsForGrid()
    {
        var currentIds = _config.Orchestrator.Roles
            .Select(r => (r.PromptId ?? "").Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && !string.Equals(s, "default", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = _catalogPublic.Select(e => new PromptItem
        {
            Id = e.Id,
            Display = e.Display,
            Description = e.Description
        }).ToList();

        items.Insert(0, new PromptItem { Id = "", Display = "(unset)", Description = "" });

        foreach (var id in currentIds)
        {
            if (items.Any(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)))
                continue;

            var e = _catalogAll.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            items.Add(e != null
                ? new PromptItem { Id = e.Id, Display = $"[hidden] {e.Display}", Description = e.Description }
                : new PromptItem { Id = id, Display = $"[missing] {id}", Description = "Not in manifest.json (manifest is authoritative)." });
        }

        return items
            .Take(1)
            .Concat(items.Skip(1).OrderBy(x => x.Display, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();

        var promptItems = BuildPromptItemsForGrid();
        var promptCol = (DataGridViewComboBoxColumn)_grid.Columns[3];
        promptCol.DataSource = promptItems;
        promptCol.DisplayMember = nameof(PromptItem.Display);
        promptCol.ValueMember = nameof(PromptItem.Id);

        foreach (var r in _config.Orchestrator.Roles)
        {
            var promptId = (r.PromptId ?? "").Trim();
            if (string.Equals(promptId, "default", StringComparison.OrdinalIgnoreCase))
                promptId = "";

            _grid.Rows.Add(r.Role, r.Provider, r.Model, promptId);
        }

        if (_grid.Rows.Count > 0)
            _grid.Rows[0].Selected = true;
    }

    private void ApplyPromptChangeFromGrid(int rowIndex)
    {
        var roleName = _grid.Rows[rowIndex].Cells[0].Value?.ToString() ?? "";
        var role = _config.Orchestrator.Roles.FirstOrDefault(r =>
            string.Equals(r.Role, roleName, StringComparison.OrdinalIgnoreCase));
        if (role == null) return;

        var newId = _grid.Rows[rowIndex].Cells[3].Value?.ToString() ?? "";
        role.PromptId = string.IsNullOrWhiteSpace(newId) ? "default" : newId.Trim();

        AutoSave.Touch("orchestrator.prompt.combo");
    }

    private string FindDescription(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";
        var e = _catalogAll.FirstOrDefault(x => string.Equals(x.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
        return e?.Description ?? "";
    }

    private OrchestratorRoleConfig? SelectedRole()
    {
        if (_grid.SelectedRows.Count != 1) return null;
        var roleName = _grid.SelectedRows[0].Cells[0].Value?.ToString() ?? "";
        return _config.Orchestrator.Roles.FirstOrDefault(r =>
            string.Equals(r.Role, roleName, StringComparison.OrdinalIgnoreCase));
    }

    private void SyncRolePurposeEditor()
    {
        var role = SelectedRole();
        if (role == null)
        {
            _rolePurpose.Enabled = false;
            _rolePurpose.Text = "";
            return;
        }

        _rolePurpose.Enabled = true;

        // avoid feedback loop
        var text = role.RolePurpose ?? "";
        if (!string.Equals(_rolePurpose.Text, text, StringComparison.Ordinal))
            _rolePurpose.Text = text;
    }

    private void EditSelectedRole()
    {
        var role = SelectedRole();
        if (role == null) return;

        using var dlg = new Form
        {
            Text = $"Edit Role: {role.Role}",
            StartPosition = FormStartPosition.CenterParent,
            Width = 700,
            Height = 220,
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
        provider.SelectedItem = role.Provider;

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
        RefreshGrid();
    }

    private void EditSelectedPrompt()
    {
        var role = SelectedRole();
        if (role == null) return;

        // Reload catalog in case user changed BlueprintTemplates Folder on General page.
        LoadCatalog();
        RefreshCatalogStatus();

        using var dlg = new BlueprintPromptPickerForm(
            title: $"BlueprintTemplates: {role.Role}",
            catalog: _catalogAll,
            roleName: role.Role,
            currentId: role.PromptId);

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var chosen = (dlg.SelectedPromptId ?? "").Trim();
        role.PromptId = string.IsNullOrWhiteSpace(chosen) ? "default" : chosen;

        AutoSave.Touch("orchestrator.prompt.picker");
        RefreshGrid();
    }
}
