#nullable enable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RahBuilder.Ui;

public sealed class ChatComposerControl : UserControl
{
    private readonly RichTextBox _input;
    private readonly Button _send;

    private readonly int _minHeight = 34;
    private readonly int _maxHeight = 180;

    public event Action<string>? SendRequested;

    public ChatComposerControl()
    {
        Dock = DockStyle.Bottom;
        Padding = new Padding(6);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

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

        root.Controls.Add(_input, 0, 0);
        root.Controls.Add(_send, 1, 0);

        Controls.Add(root);

        Height = _minHeight + Padding.Vertical;
    }

    public void FocusInput() => _input.Focus();

    public void SetEnabled(bool enabled)
    {
        _input.Enabled = enabled;
        _send.Enabled = enabled;
    }

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

    private void ReflowHeight()
    {
        var lines = Math.Max(1, _input.Lines.Length);
        var lineH = TextRenderer.MeasureText("X", _input.Font).Height;

        var desired = (lines * lineH) + 14;
        var clamped = Math.Max(_minHeight, Math.Min(_maxHeight, desired));

        if (_input.Height != clamped)
        {
            _input.Height = clamped;
            Height = clamped + Padding.Vertical;
        }
    }
}
