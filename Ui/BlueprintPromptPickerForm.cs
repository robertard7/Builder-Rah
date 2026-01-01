// C:\dev\rah\Rah-Builder\Ui\BlueprintPromptPickerForm.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RahBuilder.Tools.BlueprintTemplates;

namespace RahBuilder.Ui;

public sealed class BlueprintPromptPickerForm : Form
{
    private sealed class Row
    {
        public string Id { get; init; } = "";
        public string Display { get; init; } = "";
        public BlueprintCatalogEntry Entry { get; init; } = null!;
        public override string ToString() => Display;
    }

    private readonly TextBox _filter;
    private readonly ComboBox _scope;
    private readonly ListBox _list;
    private readonly TextBox _details;
    private readonly Button _ok;
    private readonly Button _cancel;

    private readonly IReadOnlyList<BlueprintCatalogEntry> _all;
    private readonly IReadOnlyList<BlueprintCatalogEntry> _publicOnly;

    public string? SelectedPromptId { get; private set; }

    public BlueprintPromptPickerForm(string title, IReadOnlyList<BlueprintCatalogEntry> catalog, string roleName, string? currentId)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        Width = 980;
        Height = 680;
        MinimizeBox = false;
        MaximizeBox = true;

        _all = catalog ?? Array.Empty<BlueprintCatalogEntry>();
        _publicOnly = BlueprintCatalog.PublicOnly(_all);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Select BlueprintTemplates template ID for role: {roleName}",
            AutoSize = true
        };

        var topRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _scope = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220
        };

        _scope.Items.Add($"Public ({_publicOnly.Count})");
        _scope.Items.Add($"All ({_all.Count})");
        _scope.SelectedIndex = 0;
        _scope.SelectedIndexChanged += (_, __) => RefreshList();

        _filter = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Search id/title/tags/kind..."
        };
        _filter.TextChanged += (_, __) => RefreshList();

        var clear = new Button { Text = "Clear", AutoSize = true };
        clear.Click += (_, __) => { _filter.Text = ""; _filter.Focus(); };

        topRow.Controls.Add(_scope, 0, 0);
        topRow.Controls.Add(_filter, 1, 0);
        topRow.Controls.Add(clear, 2, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 430
        };

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10f),
            HorizontalScrollbar = true
        };
        _list.SelectedIndexChanged += (_, __) => UpdateDetails();
        _list.DoubleClick += (_, __) => AcceptSelection();
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AcceptSelection();
            }
        };

        _details = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 10f)
        };

        split.Panel1.Controls.Add(_list);
        split.Panel2.Controls.Add(_details);

        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        _ok = new Button { Text = "OK", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        _cancel = new Button { Text = "Cancel", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };

        _ok.Click += (_, __) => AcceptSelection();
        _cancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };

        btnRow.Controls.Add(_ok);
        btnRow.Controls.Add(_cancel);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(topRow, 0, 1);
        root.Controls.Add(split, 0, 2);
        root.Controls.Add(btnRow, 0, 3);

        Controls.Add(root);

        RefreshList();

        // Select current if present
        if (!string.IsNullOrWhiteSpace(currentId))
        {
            var cid = currentId.Trim();
            for (int i = 0; i < _list.Items.Count; i++)
            {
                if (_list.Items[i] is Row r && string.Equals(r.Id, cid, StringComparison.OrdinalIgnoreCase))
                {
                    _list.SelectedIndex = i;
                    break;
                }
            }
        }

        if (_list.SelectedIndex < 0 && _list.Items.Count > 0)
            _list.SelectedIndex = 0;
    }

    private void RefreshList()
    {
        var useAll = _scope.SelectedIndex == 1;
        IEnumerable<BlueprintCatalogEntry> src = useAll ? _all : _publicOnly;

        var q = (_filter.Text ?? "").Trim();
        if (q.Length > 0)
        {
            src = src.Where(e =>
                e.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Kind.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        // Don’t “role filter” by deleting items. Just sort to make relevant ones float upward.
        src = RankByRelevance(src, q);

        var rows = src.Select(e => new Row
        {
            Id = e.Id,
            Display = e.Display,
            Entry = e
        }).ToList();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var r in rows)
            _list.Items.Add(r);
        _list.EndUpdate();

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
        else
            _details.Text = useAll
                ? "No templates matched your search."
                : "No PUBLIC templates matched. Switch scope to All if you need internal ones.";
    }

    private static IEnumerable<BlueprintCatalogEntry> RankByRelevance(IEnumerable<BlueprintCatalogEntry> src, string q)
    {
        // If no query, just keep original ordering.
        if (string.IsNullOrWhiteSpace(q)) return src;

        return src.OrderByDescending(e =>
        {
            var score = 0;
            if (e.Id.Contains(q, StringComparison.OrdinalIgnoreCase)) score += 5;
            if (e.Title.Contains(q, StringComparison.OrdinalIgnoreCase)) score += 3;
            if (e.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase))) score += 2;
            if (e.Kind.Contains(q, StringComparison.OrdinalIgnoreCase)) score += 1;
            return score;
        });
    }

    private void UpdateDetails()
    {
        if (_list.SelectedItem is not Row r)
        {
            _details.Text = "";
            return;
        }

        var e = r.Entry;
        _details.Text =
            $"ID: {e.Id}\r\n" +
            $"Kind: {e.Kind}\r\n" +
            $"Visibility: {e.Visibility}\r\n" +
            $"Priority: {e.Priority}\r\n" +
            $"Tags: {(e.Tags.Count == 0 ? "" : string.Join(", ", e.Tags))}\r\n" +
            $"File: {e.File}\r\n" +
            "\r\n" +
            (string.IsNullOrWhiteSpace(e.Title) ? "" : (e.Title + "\r\n\r\n")) +
            (string.IsNullOrWhiteSpace(e.Description) ? "(No description found in template file.)" : e.Description);
    }

    private void AcceptSelection()
    {
        if (_list.SelectedItem is not Row r)
            return;

        SelectedPromptId = r.Id.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }
}
