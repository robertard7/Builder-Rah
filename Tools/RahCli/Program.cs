#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahBuilder.Workflow;
using RahOllamaOnly.Tracing;

namespace RahCli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        if (args.Contains("--headless", StringComparer.OrdinalIgnoreCase))
        {
            RunHeadless();
            return 0;
        }

        var verb = args[0].ToLowerInvariant();
        if (verb == "session")
            return HandleSession(args.Skip(1).ToArray());
        if (verb == "run")
            return HandleRun(args.Skip(1).ToArray());

        PrintHelp();
        return 1;
    }

    private static int HandleSession(string[] args)
    {
        if (args.Length == 0) return 1;
        var cmd = args[0].ToLowerInvariant();
        var store = new SessionStore();

        switch (cmd)
        {
            case "list":
                WriteJson(store.ListSessions().Select(s => new { sessionId = s.SessionId, lastTouched = s.LastTouched }));
                return 0;
            case "start":
                var id = GetArgument(args, "--id") ?? Guid.NewGuid().ToString("N");
                var state = new SessionState { SessionId = id };
                store.Save(state);
                WriteJson(new { sessionId = state.SessionId, lastTouched = state.LastTouched });
                return 0;
            case "send":
                return SendMessage(store, args);
            case "export":
                var exportId = GetArgument(args, "--id") ?? "";
                var outPath = GetArgument(args, "--out") ?? "";
                if (string.IsNullOrWhiteSpace(exportId) || string.IsNullOrWhiteSpace(outPath))
                    return 1;
                store.Export(exportId, outPath);
                WriteJson(new { ok = true, sessionId = exportId, path = outPath });
                return 0;
            case "import":
                var inPath = GetArgument(args, "--in") ?? "";
                if (string.IsNullOrWhiteSpace(inPath))
                    return 1;
                var imported = store.Import(inPath);
                WriteJson(new { ok = true, sessionId = imported.SessionId });
                return 0;
        }

        return 1;
    }

    private static int SendMessage(SessionStore store, string[] args)
    {
        var id = GetArgument(args, "--id") ?? "";
        var message = GetArgument(args, "--message") ?? "";
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(message))
            return 1;

        var cfg = ConfigStore.Load();
        var trace = new RunTrace(new TracePanelTraceSink(new RahOllamaOnly.Ui.TracePanelWriter()));
        var workflow = new WorkflowFacade(cfg, trace);
        var state = store.Load(id) ?? new SessionState { SessionId = id };
        workflow.ApplySessionState(state);

        var responses = new List<string>();
        workflow.UserFacingMessage += msg =>
        {
            if (!string.IsNullOrWhiteSpace(msg))
                responses.Add(msg);
        };

        workflow.RouteUserInput(cfg, message, CancellationToken.None).GetAwaiter().GetResult();
        var updated = workflow.BuildSessionState();
        store.Save(updated);
        WriteJson(new { sessionId = updated.SessionId, responses, status = workflow.GetPublicSnapshot() });
        return 0;
    }

    private static int HandleRun(string[] args)
    {
        var id = GetArgument(args, "--id") ?? "";
        if (string.IsNullOrWhiteSpace(id))
            return 1;

        var store = new SessionStore();
        var cfg = ConfigStore.Load();
        var trace = new RunTrace(new TracePanelTraceSink(new RahOllamaOnly.Ui.TracePanelWriter()));
        var workflow = new WorkflowFacade(cfg, trace);
        var state = store.Load(id) ?? new SessionState { SessionId = id };
        workflow.ApplySessionState(state);

        var responses = new List<string>();
        workflow.UserFacingMessage += msg =>
        {
            if (!string.IsNullOrWhiteSpace(msg))
                responses.Add(msg);
        };

        workflow.ApproveAllStepsAsync(CancellationToken.None).GetAwaiter().GetResult();
        var updated = workflow.BuildSessionState();
        store.Save(updated);
        WriteJson(new { sessionId = updated.SessionId, responses, status = workflow.GetPublicSnapshot() });
        return 0;
    }

    private static void RunHeadless()
    {
        var cfg = ConfigStore.Load();
        var trace = new RunTrace(new TracePanelTraceSink(new RahOllamaOnly.Ui.TracePanelWriter()));
        var store = new SessionStore();
        var server = new HeadlessApiServer(cfg, trace, store);
        server.Start(CancellationToken.None);
        Thread.Sleep(Timeout.Infinite);
    }

    private static string? GetArgument(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }
        return null;
    }

    private static void WriteJson(object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("rah session list");
        Console.WriteLine("rah session start --id <id>");
        Console.WriteLine("rah session send --id <id> --message \"text\"");
        Console.WriteLine("rah session export --id <id> --out <file>");
        Console.WriteLine("rah session import --in <file>");
        Console.WriteLine("rah run --id <id>");
        Console.WriteLine("rah --headless");
    }
}
