#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RahBuilder.Common.Json;

public static class JsonDefaults
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented = true)
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = writeIndented
        };
    }

    public static string Serialize(object payload, bool writeIndented = true)
    {
        return JsonSerializer.Serialize(payload, CreateOptions(writeIndented));
    }
}
