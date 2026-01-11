#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RahBuilder.Workflow.Provider;

public sealed class ProviderManagerTests
{
    [Fact]
    public void UpdateEnabled_TogglesState()
    {
        var manager = new ProviderManager(new ProviderState(true, true));

        manager.UpdateEnabled(false);

        Assert.False(manager.State.Enabled);
    }

    [Fact]
    public void MarkReachable_UpdatesReachability()
    {
        var manager = new ProviderManager(new ProviderState(true, true));

        manager.MarkReachable(false, "test");

        Assert.False(manager.State.Reachable);
    }

    [Fact]
    public void Metrics_TrackUptimeAndStaleStatus()
    {
        var now = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var manager = new ProviderManager(new ProviderState(true, true), null, () => now);

        now = now.AddMinutes(6);
        var metrics = manager.GetMetricsSnapshot();

        Assert.True(metrics.IsStale);
        Assert.True(metrics.TotalUptimeSeconds >= 360);
    }

    [Fact]
    public void Diagnostics_EmitEventsOnDisable()
    {
        ProviderDiagnosticsHub.ResetEvents();
        ProviderDiagnosticEvent? captured = null;
        Action<IReadOnlyList<ProviderDiagnosticEvent>> handler = events => captured = events.LastOrDefault();
        ProviderDiagnosticsHub.EventsUpdated += handler;
        var manager = new ProviderManager(new ProviderState(true, true));

        manager.UpdateEnabled(false);

        ProviderDiagnosticsHub.EventsUpdated -= handler;
        Assert.NotNull(captured);
        Assert.Equal(ProviderEventType.Disabled, captured!.EventType);
    }

    [Fact]
    public async Task RetryAsync_RecordsMetrics()
    {
        var manager = new ProviderManager(new ProviderState(true, false));
        var before = manager.GetMetricsSnapshot();

        await manager.RetryAsync(() => Task.CompletedTask);

        var after = manager.GetMetricsSnapshot();
        Assert.True(after.RetryAttempts >= before.RetryAttempts + 1);
        Assert.True(after.RetrySuccesses >= before.RetrySuccesses + 1);
    }

    [Fact]
    public void RetryBackoff_UsesExpectedSchedule()
    {
        var schedule = ProviderManager.RetryBackoff.ToArray();

        Assert.Equal(TimeSpan.FromSeconds(1), schedule[0]);
        Assert.Equal(TimeSpan.FromSeconds(5), schedule[1]);
        Assert.Equal(TimeSpan.FromSeconds(15), schedule[2]);
        Assert.Equal(TimeSpan.FromMinutes(1), schedule[3]);
    }
}
