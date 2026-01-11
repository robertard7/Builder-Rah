#nullable enable
using System.Text.Json;
using Xunit;

namespace RahBuilder.Tests.Helpers;

public static class JsonAssert
{
    public static void Equal(string expectedJson, string actualJson)
    {
        using var expectedDoc = JsonDocument.Parse(expectedJson);
        using var actualDoc = JsonDocument.Parse(actualJson);
        Assert.Equal(expectedDoc.RootElement.ToString(), actualDoc.RootElement.ToString());
    }
}
