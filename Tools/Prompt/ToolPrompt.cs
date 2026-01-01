#nullable enable
using System;

namespace RahOllamaOnly.Tools.Prompt
{
    internal sealed class ToolPrompt
    {
        public string ToolId { get; }
        public string RawText { get; }
        public string Category { get; }

        public ToolPrompt(string toolId, string rawText, string category)
        {
            ToolId = toolId ?? throw new ArgumentNullException(nameof(toolId));
            RawText = rawText ?? throw new ArgumentNullException(nameof(rawText));
            Category = category ?? "";
        }
    }
}
