#nullable enable
using System;
using System.Text;

namespace RahOllamaOnly.Ui;

/// <summary>
/// Minimal trace backing store for UI panels.
/// </summary>
public sealed class TracePanelWriter
{
    private readonly object _gate = new();
    private readonly StringBuilder _sb = new();

    public event Action? Updated;

    public void Clear()
    {
        lock (_gate) _sb.Clear();
        Updated?.Invoke();
    }

    public void Append(string text)
    {
        if (text is null) return;
        lock (_gate) _sb.Append(text);
        Updated?.Invoke();
    }

    public void WriteLine(string line)
    {
        lock (_gate) _sb.AppendLine(line ?? string.Empty);
        Updated?.Invoke();
    }

    // Back-compat for TracePanelTraceSink.cs
    public void TraceLine(string line) => WriteLine(line);

    public string Snapshot()
    {
        lock (_gate) return _sb.ToString();
    }
}
