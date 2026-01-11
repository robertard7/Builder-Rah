#nullable enable
using System.IO;
using Xunit;

namespace RahBuilder.Tests.Contracts;

public sealed class OpenApiContractTests
{
    [Fact]
    public void OpenApiContainsSessions()
    {
        var text = File.ReadAllText("openapi.yaml");
        Assert.Contains("/sessions", text);
    }
}
