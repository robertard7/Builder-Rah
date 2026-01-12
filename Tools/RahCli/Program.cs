#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Common.Json;
using RahBuilder.Common.Text;
using RahBuilder.Headless.Api;
using RahBuilder.Settings;
using RahBuilder.Workflow;
using RahCli.Commands;
using RahCli.Output;
using RahOllamaOnly.Tracing;

namespace RahCli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return ExitCodes.UserError;
        }

        var jsonOutput = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var filtered = args.Where(a => !string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (filtered.Contains("--headless", StringComparer.OrdinalIgnoreCase))
        {
            RunHeadless();
            return ExitCodes.Success;
        }

        var verb = filtered[0].ToLowerInvariant();
        var ctx = new CommandContext(jsonOutput, ConfigStore.Load());
        if (verb == "session")
            return HandleSession(ctx, filtered.Skip(1).ToArray());
        if (verb == "run")
            return HandleRun(ctx, filtered.Skip(1).ToArray());
        if (verb == "provider")
            return HandleProvider(ctx, filtered.Skip(1).ToArray());
        if (verb == "resilience")
            return HandleResilience(ctx, filtered.Skip(1).ToArray());

        PrintHelp();
        WriteError(ctx, ApiError.BadRequest("unknown_command"));
        return ExitCodes.UserError;
    }

    private static int HandleSession(CommandContext context, string[] args)
    {
        if (args.Length == 0)
        {
            WriteError(context, ApiError.BadRequest("missing_session_command"));
            return ExitCodes.UserError;
        }
        var cmd = args[0].ToLowerInvariant();
        var store = new SessionStore();

        switch (cmd)
        {
            case "list":
                WritePayload(context, store.ListSessions().Select(s => new { sessionId = s.SessionId, lastTouched = s.LastTouched }));
                return ExitCodes.Success;
            case "start":
                var id = GetArgument(args, "--id") ?? Guid.NewGuid().ToString("N");
                var state = new SessionState { SessionId = id };
                store.Save(state);
                WritePayload(context, new { sessionId = state.SessionId, lastTouched = state.LastTouched });
                return ExitCodes.Success;
            case "send":
                return SendMessage(context, store, args);
            case "export":
                var exportId = GetArgument(args, "--id") ?? "";
                var outPath = GetArgument(args, "--out") ?? "";
                if (string.IsNullOrWhiteSpace(exportId) || string.IsNullOrWhiteSpace(outPath))
                {
                    WriteError(context, ApiError.BadRequest("export_requires_id_and_out"));
                    return ExitCodes.UserError;
                }
                store.Export(exportId, outPath);
                WritePayload(context, new { ok = true, sessionId = exportId, path = outPath });
                return ExitCodes.Success;
            case "import":
                var inPath = GetArgument(args, "--in") ?? "";
                if (string.IsNullOrWhiteSpace(inPath))
                {
                    WriteError(context, ApiError.BadRequest("import_requires_in"));
                    return ExitCodes.UserError;
                }
                var imported = store.Import(inPath);
                WritePayload(context, new { ok = true, sessionId = imported.SessionId });
                return ExitCodes.Success;
            case "status":
                var statusId = GetArgument(args, "--id") ?? "";
                return SessionStatusCommand.Execute(context, statusId, context.JsonOutput);
            case "plan":
                var planId = GetArgument(args, "--id") ?? "";
                return SessionPlanCommand.Execute(context, planId, context.JsonOutput);
            case "cancel":
                var cancelId = GetArgument(args, "--id") ?? "";
                return SessionCancelCommand.Execute(context, cancelId, context.JsonOutput);
            case "delete":
                var deleteId = GetArgument(args, "--id") ?? "";
                return SessionDeleteCommand.Execute(context, deleteId, context.JsonOutput);
        }

        WriteError(context, ApiError.BadRequest("unknown_session_command"));
        return ExitCodes.UserError;
    }

    private static int HandleProvider(CommandContext context, string[] args)
    {
        if (args.Length == 0)
        {
            WriteError(context, ApiError.BadRequest("missing_provider_command"));
            return ExitCodes.UserError;
        }
        var cmd = args[0].ToLowerInvariant();
        switch (cmd)
        {
            case "metrics":
                return ProviderMetricsCommand.Execute(context, context.JsonOutput);
            case "events":
                return ProviderEventsCommand.Execute(context, context.JsonOutput);
        }

        WriteError(context, ApiError.BadRequest("unknown_provider_command"));
        return ExitCodes.UserError;
    }

    private static int HandleResilience(CommandContext context, string[] args)
    {
        if (args.Length == 0)
        {
            WriteError(context, ApiError.BadRequest("missing_resilience_command"));
            return ExitCodes.UserError;
        }

        var cmd = args[0].ToLowerInvariant();
        switch (cmd)
        {
            case "metrics":
                return ResilienceMetricsCommand.Execute(context, args.Skip(1).ToArray(), context.JsonOutput);
            case "watch":
                return ResilienceMetricsCommand.Execute(context, new[] { "--watch" }, context.JsonOutput);
            case "reset":
                return ResilienceResetCommand.Execute(context, context.JsonOutput);
        }

        WriteError(context, ApiError.BadRequest("unknown_resilience_command"));
        return ExitCodes.UserError;
    }

    private static int SendMessage(CommandContext context, SessionStore store, string[] args)
    {
        var id = GetArgument(args, "--id") ?? "";
        var message = GetArgument(args, "--message") ?? "";
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(message))
        {
            WriteError(context, ApiError.BadRequest("send_requires_id_and_message"));
            return ExitCodes.UserError;
        }

        var trace = new RunTrace(new TracePanelTraceSink(new RahOllamaOnly.Ui.TracePanelWriter()));
        var workflow = new WorkflowFacade(context.Config, trace);
        var state = store.Load(id) ?? new SessionState { SessionId = id };
        workflow.ApplySessionState(state);

        var responses = new List<string>();
        workflow.UserFacingMessage += msg =>
        {
            if (!string.IsNullOrWhiteSpace(msg))
                responses.Add(UserSafeText.Sanitize(msg));
        };

        workflow.RouteUserInput(context.Config, message, CancellationToken.None).GetAwaiter().GetResult();
        var updated = workflow.BuildSessionState();
        store.Save(updated);
        WritePayload(context, new { sessionId = updated.SessionId, responses, status = workflow.GetPublicSnapshot() });
        return ExitCodes.Success;
    }

    private static int HandleRun(CommandContext context, string[] args)
    {
        var id = GetArgument(args, "--id") ?? "";
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError(context, ApiError.BadRequest("run_requires_id"));
            return ExitCodes.UserError;
        }

        var store = new SessionStore();
        var trace = new RunTrace(new TracePanelTraceSink(new RahOllamaOnly.Ui.TracePanelWriter()));
        var workflow = new WorkflowFacade(context.Config, trace);
        var state = store.Load(id) ?? new SessionState { SessionId = id };
        workflow.ApplySessionState(state);

        var responses = new List<string>();
        workflow.UserFacingMessage += msg =>
        {
            if (!string.IsNullOrWhiteSpace(msg))
                responses.Add(UserSafeText.Sanitize(msg));
        };

        workflow.ApproveAllStepsAsync(CancellationToken.None).GetAwaiter().GetResult();
        var updated = workflow.BuildSessionState();
        store.Save(updated);
        WritePayload(context, new { sessionId = updated.SessionId, responses, status = workflow.GetPublicSnapshot() });
        return ExitCodes.Success;
    }

    private static void RunHeadless()
    {
        var trace = new RunTrace(new TracePanelTraceSink(new RahOllamaOnly.Ui.TracePanelWriter()));
        var store = new SessionStore();
        var server = new HeadlessApiServer(ConfigStore.Load(), trace, store);
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

    private static void WritePayload(CommandContext context, object payload)
    {
        if (context.JsonOutput)
            JsonOutput.Write(payload);
        else
            TextOutput.Write(JsonDefaults.Serialize(payload));
    }

    private static void WriteError(CommandContext context, ApiError error)
    {
        if (!context.JsonOutput)
            return;
        JsonOutput.WriteError(error);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("rah session list");
        Console.WriteLine("rah session start --id <id>");
        Console.WriteLine("rah session send --id <id> --message \"text\"");
        Console.WriteLine("rah session status --id <id>");
        Console.WriteLine("rah session plan --id <id>");
        Console.WriteLine("rah session cancel --id <id>");
        Console.WriteLine("rah session delete --id <id>");
        Console.WriteLine("rah session export --id <id> --out <file>");
        Console.WriteLine("rah session import --in <file>");
        Console.WriteLine("rah run --id <id>");
        Console.WriteLine("rah provider metrics");
        Console.WriteLine("rah provider events");
        Console.WriteLine("rah resilience metrics");
        Console.WriteLine("rah resilience metrics --watch");
        Console.WriteLine("rah resilience watch");
        Console.WriteLine("rah resilience reset");
        Console.WriteLine("rah --headless");
    }
}
