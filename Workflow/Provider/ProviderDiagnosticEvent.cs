#nullable enable
using System;
using System.Collections.Generic;

namespace RahBuilder.Workflow.Provider;

public enum ProviderEventType
{
    Enabled,
    Disabled,
    OfflineDetected,
    RetryAttempt,
    RetrySuccess,
    RetryFail
}

public sealed record ProviderDiagnosticEvent(
    ProviderEventType EventType,
    DateTimeOffset Timestamp,
    string Message,
    IReadOnlyDictionary<string, object>? Metadata);
