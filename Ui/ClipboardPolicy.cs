#nullable enable
using System;
using System.Windows.Forms;

namespace RahBuilder.Ui;

/// <summary>
/// Centralized clipboard + shortcuts policy so you don't duplicate dead code everywhere.
/// Applies to TextBox/RichTextBox/etc (TextBoxBase).
/// </summary>
public static class ClipboardPolicy
{
    public static void Apply(Control root)
    {
        if (root == null) return;
        ApplyRecursive(root);
    }

    private static void ApplyRecursive(Control c)
    {
        if (c is TextBoxBase tb)
        {
            tb.ShortcutsEnabled = true;

            // Attach a context menu if none exists (keeps this deterministic and non-invasive).
            if (tb.ContextMenuStrip == null)
            {
                var menu = new ContextMenuStrip();

                var cut = new ToolStripMenuItem("Cut", null, (_, _) => tb.Cut());
                var copy = new ToolStripMenuItem("Copy", null, (_, _) => tb.Copy());
                var paste = new ToolStripMenuItem("Paste", null, (_, _) => tb.Paste());
                var selAll = new ToolStripMenuItem("Select All", null, (_, _) => tb.SelectAll());

                menu.Opening += (_, e) =>
                {
                    var canEdit = !tb.ReadOnly && tb.Enabled;
                    cut.Enabled = canEdit && tb.SelectionLength > 0;
                    paste.Enabled = canEdit && Clipboard.ContainsText();
                    copy.Enabled = tb.SelectionLength > 0;
                    selAll.Enabled = tb.TextLength > 0;
                };

                menu.Items.AddRange(new ToolStripItem[] { cut, copy, paste, new ToolStripSeparator(), selAll });
                tb.ContextMenuStrip = menu;
            }
        }

        foreach (Control child in c.Controls)
            ApplyRecursive(child);
    }
}
