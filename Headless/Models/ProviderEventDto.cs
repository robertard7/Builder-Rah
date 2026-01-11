#nullable enable
using System;
using System.Collections.Generic;

namespace RahBuilder.Headless.Models;

public sealed record ProviderEventDto(
    string EventType,
    DateTimeOffset Timestamp,
    string Message,
    IReadOnlyDictionary<string, object>? Metadata);
