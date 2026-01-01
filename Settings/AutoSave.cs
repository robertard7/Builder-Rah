#nullable enable
using System;
using System.Windows.Forms;

namespace RahBuilder.Settings;

public static class AutoSave
{
    private static System.Windows.Forms.Timer? _timer;
    private static Action? _save;
    private static int _debounceMs = 350;

    public static void Wire(Control root, Action saveAction, int debounceMs = 350)
    {
        _save = saveAction ?? throw new ArgumentNullException(nameof(saveAction));
        _debounceMs = Math.Max(100, debounceMs);

        _timer?.Stop();
        _timer?.Dispose();

        _timer = new System.Windows.Forms.Timer { Interval = _debounceMs };
        _timer.Tick += (_, __) =>
        {
            _timer!.Stop();
            try { _save?.Invoke(); }
            catch { }
        };

        AttachRecursive(root);
        root.ControlAdded += (_, e) =>
        {
            if (e.Control != null) AttachRecursive(e.Control);
        };
    }

    public static void Touch() => Arm();
    public static void Touch(string _reason) => Arm();

    private static void Arm()
    {
        if (_timer == null || _save == null) return;
        _timer.Stop();
        _timer.Interval = _debounceMs;
        _timer.Start();
    }

    private static void AttachRecursive(Control c)
    {
        if (c is TextBoxBase tb)
            tb.TextChanged += (_, __) => Arm();
        else
            c.TextChanged += (_, __) => Arm();

        if (c is CheckBox cb)
            cb.CheckedChanged += (_, __) => Arm();

        if (c is ComboBox combo)
            combo.SelectedIndexChanged += (_, __) => Arm();

        foreach (Control child in c.Controls)
            AttachRecursive(child);
    }
}
