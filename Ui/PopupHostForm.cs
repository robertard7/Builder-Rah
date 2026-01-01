#nullable enable
using System;
using System.Windows.Forms;

namespace RahBuilder.Ui;

public sealed class PopupHostForm : Form
{
    public PopupHostForm(string title, Control content, bool enableClipboardShortcuts)
    {
        Text = title;
        Width = 900;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;

        content.Dock = DockStyle.Fill;
        Controls.Add(content);

        if (enableClipboardShortcuts)
            ClipboardPolicy.Apply(this);
    }
}
