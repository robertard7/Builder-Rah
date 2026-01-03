using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace RahBuilder;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--headless", StringComparer.OrdinalIgnoreCase))
        {
            RunHeadless();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void RunHeadless()
    {
        var writer = new RahOllamaOnly.Ui.TracePanelWriter();
        var trace = new RahOllamaOnly.Tracing.RunTrace(new RahOllamaOnly.Tracing.TracePanelTraceSink(writer));
        var cfg = RahBuilder.Settings.ConfigStore.Load();
        var workflow = new RahBuilder.Workflow.WorkflowFacade(cfg, trace);
        trace.Emit("[headless] Provider API ready. Use /api/jobs to submit work.");
        Thread.Sleep(Timeout.Infinite);
        _ = workflow;
    }
}
