#nullable enable
using System.Collections.Generic;

namespace RahBuilder.Workflow;

public sealed record IntentDocumentV1(
    string Version,
    string Goal,
    string Context,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Missing,
    bool Ready,
    string Clarification);
