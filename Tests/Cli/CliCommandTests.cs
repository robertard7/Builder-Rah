#nullable enable
using RahBuilder.Tests.Helpers;
using RahCli;
using Xunit;

namespace RahBuilder.Tests.Cli;

public sealed class CliCommandTests
{
    [Fact]
    public void NoArgs_ReturnsUserError()
    {
        var result = CliRunner.Run();
        Assert.Equal(ExitCodes.UserError, result.ExitCode);
    }
}
