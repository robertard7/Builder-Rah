#nullable enable
using System.IO;
using Xunit;

namespace RahBuilder.Tests.Contracts;

public sealed class OpenApiValidationTests
{
    [Fact]
    public void OpenApiFileExists()
    {
        Assert.True(File.Exists("openapi.yaml"));
    }
}
