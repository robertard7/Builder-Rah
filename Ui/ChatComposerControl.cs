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
    private readonly Button _attachmentsButton;
    private readonly Button _photosButton;
    private readonly Button _send;
    private readonly FlowLayoutPanel _chips;
    private readonly Label _status;
    private readonly Label _providerHint;
    private readonly Button _retryProvider;

    private AttachmentInbox _inbox;
    private readonly List<AttachmentInbox.AttachmentEntry> _attachments = new();

    private const int MaxVisibleLines = 20;
    private readonly int _minHeight = 34;

    public event Action<string>? SendRequested;
    public event Action<IReadOnlyList<AttachmentInbox.AttachmentEntry>>? AttachmentsChanged;
    public event Action? RetryProviderRequested;

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
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 4)
        };

        _attachmentsButton = new Button
        {
            Text = "Attachments",
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 0, 6, 0)
        };

        _photosButton = new Button
        {
            Text = "Photos",
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 0, 6, 0)
        };

        _attachmentsButton.Click += (_, _) => ChooseAttachments();
        _photosButton.Click += (_, _) => ChoosePhotos();

        actionRow.Controls.Add(_attachmentsButton);
        actionRow.Controls.Add(_photosButton);

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
        _providerHint = new Label
        {
            AutoSize = true,
            ForeColor = Color.Firebrick,
            Text = "Provider offline — check settings or retry.",
            Visible = false
        };
        _retryProvider = new Button
        {
            Text = "Retry Provider",
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 2),
            Visible = false
        };
        _retryProvider.Click += (_, _) => RetryProviderRequested?.Invoke();

        statusPanel.Controls.Add(_status);
        statusPanel.Controls.Add(_providerHint);
        statusPanel.Controls.Add(_retryProvider);

        var composerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        composerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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

        _send = new Button
        {
            Text = "Send",
            AutoSize = true,
            Padding = new Padding(14, 8, 14, 8),
            Margin = new Padding(6, 0, 0, 0)
        };

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
        composerRow.Controls.Add(_send, 1, 0);

        root.Controls.Add(actionRow, 0, 0);
        root.Controls.Add(_chips, 0, 1);
        root.Controls.Add(statusPanel, 0, 2);
        root.Controls.Add(composerRow, 0, 3);

        Controls.Add(root);

        Height = _minHeight + Padding.Vertical + _chips.Height + statusPanel.Height;
        RefreshAttachments(_inbox.List());
    }

    public void FocusInput() => _input.Focus();

    public void SetEnabled(bool enabled)
    {
        _input.Enabled = enabled;
        _attachmentsButton.Enabled = enabled;
        _photosButton.Enabled = enabled;
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

    public void SetProviderOffline(bool isOffline)
    {
        _providerHint.Visible = isOffline;
        _retryProvider.Visible = isOffline;
    }

    private void FireSend()
    {
        var text = (_input.Text ?? "").Trim();
        if (text.Length == 0) return;

        Clear();
        SendRequested?.Invoke(text);
    }

    private void ChooseAttachments()
    {
        ChooseFiles("Attach files", "All files (*.*)|*.*");
    }

    private void ChoosePhotos()
    {
        ChooseFiles(
            "Attach photos",
            "Image files (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tiff)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tiff|All files (*.*)|*.*");
    }

    private void ChooseFiles(string title, string filter)
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Title = title,
            Filter = filter
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
        var lines = Math.Max(1, _input.GetLineFromCharIndex(_input.TextLength) + 1);
        var lineH = TextRenderer.MeasureText("X", _input.Font).Height;
        var targetLines = Math.Min(MaxVisibleLines, lines);
        var desired = (targetLines * lineH) + 14;
        var clamped = Math.Max(_minHeight, desired);

        if (_input.Height != clamped)
            _input.Height = clamped;
    }
}
