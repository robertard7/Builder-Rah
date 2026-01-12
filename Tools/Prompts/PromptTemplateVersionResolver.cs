#nullable enable
using System;

namespace RahOllamaOnly.Tools.Prompts;

public static class PromptTemplateVersionResolver
{
    public const int CurrentVersion = 1;

    public static int Resolve(int version)
    {
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version), "Prompt template version must be greater than zero.");
        if (version > CurrentVersion)
            throw new NotSupportedException($"Prompt template version {version} is not supported.");

        return version;
    }

    public static bool IsSupported(int version)
        => version > 0 && version <= CurrentVersion;
}
