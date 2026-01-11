#nullable enable
using RahBuilder.Tests.Helpers;
using RahCli;
using Xunit;

namespace RahBuilder.Tests.Cli;

public sealed class CliJsonOutputTests
{
    [Fact]
    public void InvalidCommand_ReturnsUserError()
    {
        var result = CliRunner.Run("--json", "unknown");
        Assert.Equal(ExitCodes.UserError, result.ExitCode);
    }
}
