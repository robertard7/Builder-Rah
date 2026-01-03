using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RahBuilder;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--headless", StringComparer.OrdinalIgnoreCase))
        {
            RunHeadless(args);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void RunHeadless(string[] args)
    {
        var writer = new RahOllamaOnly.Ui.TracePanelWriter();
        var trace = new RahOllamaOnly.Tracing.RunTrace(new RahOllamaOnly.Tracing.TracePanelTraceSink(writer));
        var cfg = RahBuilder.Settings.ConfigStore.Load();
        var workflow = new RahBuilder.Workflow.WorkflowFacade(cfg, trace);
        var textArg = GetArgument(args, "--text");
        var outArg = GetArgument(args, "--output");

        if (string.IsNullOrWhiteSpace(textArg))
        {
            trace.Emit("[headless] Provider API ready. Use /api/jobs to submit work.");
            Thread.Sleep(Timeout.Infinite);
            return;
        }

        var done = new ManualResetEventSlim(false);
        workflow.OutputCardProduced += card =>
        {
            if (card.Kind == RahBuilder.Workflow.OutputCardKind.ProgramZip || card.Kind == RahBuilder.Workflow.OutputCardKind.Final)
                done.Set();
        };

        Task.Run(() => workflow.RouteUserInput(cfg, textArg, CancellationToken.None));
        done.Wait(TimeSpan.FromMinutes(5));

        var artifacts = workflow.GetArtifacts().LastOrDefault();
        if (artifacts != null && !string.IsNullOrWhiteSpace(outArg))
        {
            try
            {
                var target = System.IO.Path.Combine(outArg, System.IO.Path.GetFileName(artifacts.ZipPath));
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                System.IO.File.Copy(artifacts.ZipPath, target, true);
                trace.Emit("[headless] saved artifact zip to " + target);
            }
            catch (Exception ex)
            {
                trace.Emit("[headless:error] " + ex.Message);
            }
        }
    }

    private static string? GetArgument(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }
        return null;
    }
}
