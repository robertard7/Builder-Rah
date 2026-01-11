#nullable enable
using System.IO;
using Xunit;

namespace RahBuilder.Tests.Headless;

public sealed class OpenApiContractTests
{
    [Fact]
    public void OpenApiSpecExists()
    {
        Assert.True(File.Exists("openapi.yaml"));
    }
}
