#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RahBuilder.Workflow;

public sealed class ConversationMemory
{
    public List<string> UserMessages { get; } = new();
    public List<string> AssistantMessages { get; } = new();
    public List<string> ClarificationQuestions { get; } = new();
    public Dictionary<string, string> ClarificationAnswers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> PlanNotes { get; } = new();
    public List<string> ToolOutputs { get; } = new();

    public void Clear()
    {
        UserMessages.Clear();
        AssistantMessages.Clear();
        ClarificationQuestions.Clear();
        ClarificationAnswers.Clear();
        PlanNotes.Clear();
        ToolOutputs.Clear();
    }

    public void AddUserMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            UserMessages.Add(message.Trim());
    }

    public void AddAssistantMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            AssistantMessages.Add(message.Trim());
    }

    public void AddClarificationQuestion(string question)
    {
        if (!string.IsNullOrWhiteSpace(question))
            ClarificationQuestions.Add(question.Trim());
    }

    public void AddClarificationAnswer(string field, string answer)
    {
        field ??= "";
        if (string.IsNullOrWhiteSpace(answer))
            return;
        ClarificationAnswers[field.Trim()] = answer.Trim();
    }

    public bool HasAnswerFor(string field)
    {
        field ??= "";
        return ClarificationAnswers.ContainsKey(field.Trim());
    }

    public void AddPlanNote(string note)
    {
        if (!string.IsNullOrWhiteSpace(note))
            PlanNotes.Add(note.Trim());
    }

    public void AddToolOutput(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            ToolOutputs.Add(text.Trim());
    }

    public object ToSnapshot() => new
    {
        users = UserMessages.ToList(),
        assistants = AssistantMessages.ToList(),
        clarifications = ClarificationQuestions.ToList(),
        clarificationAnswers = new Dictionary<string, string>(ClarificationAnswers, StringComparer.OrdinalIgnoreCase),
        planNotes = PlanNotes.ToList(),
        tools = ToolOutputs.ToList()
    };

    public string Serialize() => JsonSerializer.Serialize(ToSnapshot(), new JsonSerializerOptions { WriteIndented = true });
}
