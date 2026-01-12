#nullable enable
using System;
using RahBuilder.Tests.Helpers;
using RahCli;
using RahOllamaOnly.Metrics;
using Xunit;

namespace RahBuilder.Tests.Cli;

public sealed class ResilienceCliTests
{
    [Fact]
    public void HistoryRangeCommand_WritesOutput()
    {
        ResilienceDiagnosticsHub.Reset();
        ResilienceDiagnosticsHub.Snapshot();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O");
        var end = DateTimeOffset.UtcNow.ToString("O");

        var result = CliRunner.Run("resilience", "history", "--start", start, "--end", end);
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Timestamp", result.StdOut);
    }

    [Fact]
    public void AlertsResolveCommand_AcknowledgesEvent()
    {
        ResilienceDiagnosticsHub.Reset();
        ResilienceDiagnosticsHub.AddAlertRule("retry-alert", openThreshold: 0, retryThreshold: 0, windowMinutes: 60, severity: "warning");
        ResilienceDiagnosticsHub.Snapshot();
        ResilienceDiagnosticsHub.RecordRetryAttempt("cli");
        ResilienceDiagnosticsHub.Snapshot();
        var eventId = ResilienceDiagnosticsHub.ListAlertEvents(1)[0].Id;

        var result = CliRunner.Run("resilience", "alert", "resolve", eventId);
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("acknowledged", result.StdOut, StringComparison.OrdinalIgnoreCase);
    }
}
