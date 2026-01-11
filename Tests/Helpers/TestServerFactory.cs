#nullable enable
using System;
using System.Threading;
using RahBuilder.Settings;
using RahBuilder.Workflow;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Tests.Helpers;

public sealed class TestServerFactory : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly HeadlessApiServer _server;

    public TestServerFactory(AppConfig config)
    {
        var trace = new RunTrace(new TracePanelTraceSink(new RahOllamaOnly.Ui.TracePanelWriter()));
        _server = new HeadlessApiServer(config, trace, new SessionStore());
        _server.Start(_cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _server.Stop();
        _cts.Dispose();
    }
}
