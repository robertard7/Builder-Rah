#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RahBuilder.Workflow;

namespace RahBuilder.Ui;

public sealed class ChatComposerControl : UserControl
{
    private readonly RichTextBox _input;
    private readonly Button _attach;
    private readonly Button _send;
    private readonly FlowLayoutPanel _chips;
    private readonly Label _status;

    private AttachmentInbox _inbox;
    private readonly List<AttachmentInbox.AttachmentEntry> _attachments = new();

    private readonly int _minHeight = 34;
    private readonly int _maxHeight = 180;

    public event Action<string>? SendRequested;
    public event Action<IReadOnlyList<AttachmentInbox.AttachmentEntry>>? AttachmentsChanged;

    public ChatComposerControl(AttachmentInbox inbox)
    {
        Dock = DockStyle.Bottom;
        Padding = new Padding(6);

        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));

        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _chips = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 2, 0, 4)
        };

        _status = new Label { AutoSize = true, ForeColor = Color.DimGray };
        statusPanel.Controls.Add(_status);

        var composerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        composerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        composerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        composerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _input = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10f),
            Height = _minHeight
        };

        _attach = new Button
        {
            Text = "Attach…",
            AutoSize = true,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(6, 0, 0, 0)
        };

        _send = new Button
        {
            Text = "Send",
            AutoSize = true,
            Padding = new Padding(14, 8, 14, 8),
            Margin = new Padding(6, 0, 0, 0)
        };

        _attach.Click += (_, _) => ChooseFiles();
        _send.Click += (_, _) => FireSend();

        _input.TextChanged += (_, _) => ReflowHeight();

        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Shift+Enter => newline
                if (e.Shift) return;

                // Enter => send
                e.SuppressKeyPress = true;
                FireSend();
            }
        };

        composerRow.Controls.Add(_input, 0, 0);
        composerRow.Controls.Add(_attach, 1, 0);
        composerRow.Controls.Add(_send, 2, 0);

        root.Controls.Add(_chips, 0, 0);
        root.Controls.Add(statusPanel, 0, 1);
        root.Controls.Add(composerRow, 0, 2);

        Controls.Add(root);

        Height = _minHeight + Padding.Vertical + _chips.Height + statusPanel.Height;
        RefreshAttachments(_inbox.List());
    }

    public void FocusInput() => _input.Focus();

    public void SetEnabled(bool enabled)
    {
        _input.Enabled = enabled;
        _attach.Enabled = enabled;
        _send.Enabled = enabled;
    }

    public void SetInbox(AttachmentInbox inbox)
    {
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        RefreshAttachments(_inbox.List());
    }

    public void ReloadAttachments(IReadOnlyList<AttachmentInbox.AttachmentEntry> entries) => RefreshAttachments(entries);

    public void Clear()
    {
        _input.Text = "";
        ReflowHeight();
    }

    private void FireSend()
    {
        var text = (_input.Text ?? "").Trim();
        if (text.Length == 0) return;

        Clear();
        SendRequested?.Invoke(text);
    }

    private void ChooseFiles()
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Attach files",
            Filter = "All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
            AddFiles(dlg.FileNames);
    }

    private void AddFiles(IEnumerable<string> files)
    {
        var result = _inbox.AddFiles(files);
        if (!result.Ok)
        {
            ShowStatus(result.Message, true);
            return;
        }

        RefreshAttachments(_inbox.List());
        ShowStatus($"Added {result.Added.Count} attachment(s).", false);
    }

    private void RemoveAttachment(string storedName)
    {
        var result = _inbox.Remove(storedName);
        if (!result.Ok)
        {
            ShowStatus(result.Message, true);
            return;
        }

        RefreshAttachments(_inbox.List());
        ShowStatus("Attachment removed.", false);
    }

    private void RefreshAttachments(IReadOnlyList<AttachmentInbox.AttachmentEntry> entries)
    {
        _attachments.Clear();
        if (entries != null)
            _attachments.AddRange(entries);

        _chips.Controls.Clear();

        foreach (var a in _attachments)
        {
            var chip = BuildChip(a);
            _chips.Controls.Add(chip);
        }

        AttachmentsChanged?.Invoke(_attachments.ToList());
    }

    private Control BuildChip(AttachmentInbox.AttachmentEntry entry)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = entry.Active ? Color.FromArgb(230, 230, 230) : Color.FromArgb(245, 230, 230),
            Margin = new Padding(0, 0, 6, 6),
            Padding = new Padding(6, 4, 6, 4),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var name = new Label
        {
            AutoSize = true,
            Text = $"{entry.OriginalName} ({FormatBytes(entry.SizeBytes)})",
            Padding = new Padding(0, 2, 6, 0)
        };

        var activeToggle = new CheckBox
        {
            Text = "Active",
            Checked = entry.Active,
            AutoSize = true,
            Padding = new Padding(0, 0, 6, 0)
        };
        activeToggle.CheckedChanged += (_, _) => ToggleAttachmentActive(entry.StoredName, activeToggle.Checked);

        var remove = new Button
        {
            Text = "x",
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(4, 0, 4, 0)
        };
        remove.Click += (_, _) => RemoveAttachment(entry.StoredName);

        panel.Controls.Add(name);
        panel.Controls.Add(activeToggle);
        panel.Controls.Add(remove);
        return panel;
    }

    private void ToggleAttachmentActive(string storedName, bool active)
    {
        var result = _inbox.SetActive(storedName, active);
        if (!result.Ok)
        {
            ShowStatus(result.Message, true);
            return;
        }

        RefreshAttachments(_inbox.List());
        ShowStatus(active ? "Attachment active." : "Attachment inactive.", false);
    }

    private void OnDragEnter(object? _, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? _, DragEventArgs e)
    {
        if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            AddFiles(paths.Where(File.Exists));
    }

    private void ShowStatus(string message, bool isError)
    {
        _status.Text = message ?? "";
        _status.ForeColor = isError ? Color.Firebrick : Color.DimGray;
    }

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        if (bytes >= GB) return $"{bytes / (double)GB:0.##} GB";
        if (bytes >= MB) return $"{bytes / (double)MB:0.##} MB";
        if (bytes >= KB) return $"{bytes / (double)KB:0.##} KB";
        return bytes + " B";
    }

    private void ReflowHeight()
    {
        var lines = Math.Max(1, _input.Lines.Length);
        var lineH = TextRenderer.MeasureText("X", _input.Font).Height;

        var desired = (lines * lineH) + 14;
        var clamped = Math.Max(_minHeight, Math.Min(_maxHeight, desired));

        if (_input.Height != clamped)
            _input.Height = clamped;
    }
}
