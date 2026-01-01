#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace RahBuilder.Workflow;

public static class MermaidGraphHub
{
    public static string CurrentGraph { get; private set; } = "";
    public static string CurrentHash { get; private set; } = "";

    public static event Action? Updated;

    public static void Publish(string graph)
    {
        CurrentGraph = graph ?? "";
        using var sha = SHA256.Create();
        CurrentHash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(CurrentGraph)));
        Updated?.Invoke();
    }
}
