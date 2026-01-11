#nullable enable
using System;
using System.IO;

namespace RahBuilder.Tests.Helpers;

public sealed class DataLifecycle : IDisposable
{
    public string RootPath { get; } = Path.Combine(Path.GetTempPath(), "rah-tests-" + Guid.NewGuid().ToString("N"));

    public DataLifecycle()
    {
        Directory.CreateDirectory(RootPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(RootPath, true); } catch { }
    }
}
