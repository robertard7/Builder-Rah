#nullable enable
using System;
using RahBuilder.Headless.Api;

namespace RahCli.Output;

public static class TextOutput
{
    public static void Write(object payload)
    {
        Console.WriteLine(payload);
    }

    public static void WriteError(ApiError error)
    {
        Console.Error.WriteLine($"{error.Code}: {error.Message}");
    }
}
