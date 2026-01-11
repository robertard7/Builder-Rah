#nullable enable
using System;
using RahBuilder.Common.Json;
using RahBuilder.Headless.Api;

namespace RahCli.Output;

public static class JsonOutput
{
    public static void Write(object payload)
    {
        Console.WriteLine(JsonDefaults.Serialize(new { ok = true, data = payload }));
    }

    public static void WriteError(ApiError error)
    {
        Console.Error.WriteLine(JsonDefaults.Serialize(new { ok = false, error }));
    }
}
