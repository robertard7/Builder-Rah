#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RahBuilder.Workflow;

public static class IntentValidation
{
    public static IReadOnlyList<string> Validate(IntentDocumentV1 doc)
    {
        var errors = new List<string>();
        if (doc == null)
            return new[] { "intent_missing" };
        if (!string.Equals(doc.Version, "intent.v1", StringComparison.OrdinalIgnoreCase))
            errors.Add("intent_version_invalid");
        if (string.IsNullOrWhiteSpace(doc.Goal))
            errors.Add("goal_required");
        if (doc.Actions == null || doc.Actions.Count == 0)
            errors.Add("actions_required");
        if (doc.Ready && (doc.Missing != null && doc.Missing.Count > 0))
            errors.Add("ready_with_missing");
        return errors;
    }

    public static bool IsValid(IntentDocumentV1 doc) => !Validate(doc).Any();
}
