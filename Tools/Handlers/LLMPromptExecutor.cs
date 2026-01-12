#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahOllamaOnly.Tools;

namespace RahOllamaOnly.Tools.Handlers;

public sealed class LLMPromptExecutor : IPromptExecutor
{
    private readonly AppConfig _cfg;
    private readonly string _defaultRole;

    public LLMPromptExecutor(AppConfig cfg, string defaultRole)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _defaultRole = string.IsNullOrWhiteSpace(defaultRole) ? "Orchestrator" : defaultRole;
    }

    public async Task<ExecutionResult> ExecuteAsync(string prompt, ExecutionOptions options)
    {
        if (prompt == null)
            throw new ArgumentNullException(nameof(prompt));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var role = string.IsNullOrWhiteSpace(options.Role) ? _defaultRole : options.Role;
        var attempts = Math.Max(0, options.MaxRetries) + 1;
        Exception? lastError = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            using var cts = CreateCancellation(options);
            try
            {
                var response = await LlmInvoker.InvokeChatAsync(
                    _cfg,
                    role,
                    prompt,
                    string.Empty,
                    cts.Token).ConfigureAwait(false);

                return new ExecutionResult(true, response);
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (attempt >= attempts - 1)
                    break;

                var delay = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt));
                try
                {
                    await Task.Delay(delay, options.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return new ExecutionResult(false, string.Empty, lastError?.Message ?? "Prompt execution failed.");
    }

    private CancellationTokenSource CreateCancellation(ExecutionOptions options)
    {
        if (options.Timeout.HasValue && options.Timeout.Value > TimeSpan.Zero)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
            cts.CancelAfter(options.Timeout.Value);
            return cts;
        }

        return CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
    }
}
