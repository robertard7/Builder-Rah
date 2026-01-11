#nullable enable
using System.Text.Json;
using RahBuilder.Tests.Helpers;
using RahCli;
using Xunit;

namespace RahBuilder.Tests.Cli;

public sealed class CliIntegrationTests
{
    [Fact]
    public void ProviderMetrics_CommandWorks()
    {
        var result = CliRunner.Run("--json", "provider", "metrics");
        Assert.Equal(ExitCodes.Success, result.ExitCode);
    }

    [Fact]
    public void SessionDelete_CommandWorks()
    {
        var start = CliRunner.Run("--json", "session", "start");
        Assert.Equal(ExitCodes.Success, start.ExitCode);
        using var doc = JsonDocument.Parse(start.StdOut);
        var sessionId = doc.RootElement.GetProperty("data").GetProperty("sessionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        var del = CliRunner.Run("--json", "session", "delete", "--id", sessionId!);
        Assert.Equal(ExitCodes.Success, del.ExitCode);
    }
}
