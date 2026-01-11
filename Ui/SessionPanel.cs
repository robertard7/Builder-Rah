#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RahBuilder.Workflow;

namespace RahBuilder.Ui;

public sealed class SessionPanel : UserControl
{
    private readonly SessionStore _store;
    private readonly ListBox _list;

    public event Action<SessionState>? SessionLoaded;

    public SessionPanel(SessionStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Dock = DockStyle.Fill;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _list = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
        root.Controls.Add(_list, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var refresh = new Button { Text = "Refresh", AutoSize = true };
        refresh.Click += (_, _) => Reload();

        var load = new Button { Text = "Load", AutoSize = true };
        load.Click += (_, _) => LoadSelected();

        var export = new Button { Text = "Export", AutoSize = true };
        export.Click += (_, _) => ExportSelected();

        var import = new Button { Text = "Import", AutoSize = true };
        import.Click += (_, _) => ImportSession();

        var remove = new Button { Text = "Delete", AutoSize = true };
        remove.Click += (_, _) => DeleteSelected();

        buttons.Controls.Add(refresh);
        buttons.Controls.Add(load);
        buttons.Controls.Add(export);
        buttons.Controls.Add(import);
        buttons.Controls.Add(remove);

        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);

        Reload();
    }

    private void Reload()
    {
        _list.Items.Clear();
        var sessions = _store.ListSessions();
        foreach (var s in sessions)
            _list.Items.Add($"{s.SessionId} | {s.LastTouched:O} | {s.Messages.Count} msg(s)");
    }

    private string? SelectedSessionId()
    {
        if (_list.SelectedIndex < 0) return null;
        var text = _list.SelectedItem?.ToString() ?? "";
        var parts = text.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : null;
    }

    private void LoadSelected()
    {
        var id = SelectedSessionId();
        if (string.IsNullOrWhiteSpace(id)) return;
        var state = _store.Load(id);
        if (state != null)
            SessionLoaded?.Invoke(state);
    }

    private void ExportSelected()
    {
        var id = SelectedSessionId();
        if (string.IsNullOrWhiteSpace(id)) return;
        using var dialog = new SaveFileDialog
        {
            FileName = id + ".json",
            Filter = "JSON|*.json|All files|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
            _store.Export(id, dialog.FileName);
    }

    private void ImportSession()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON|*.json|All files|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            _store.Import(dialog.FileName);
            Reload();
        }
    }

    private void DeleteSelected()
    {
        var id = SelectedSessionId();
        if (string.IsNullOrWhiteSpace(id)) return;
        _store.Delete(id);
        Reload();
    }
}
