#nullable enable
using System.Threading.Tasks;

namespace RahOllamaOnly.Tools.Handlers;

public interface IPromptExecutor
{
    Task<ExecutionResult> ExecuteAsync(string prompt, ExecutionOptions options);
}
