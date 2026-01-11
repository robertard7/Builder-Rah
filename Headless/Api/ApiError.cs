#nullable enable
using System.Collections.Generic;

namespace RahBuilder.Headless.Api;

public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object>? Details = null)
{
    public static ApiError NotFound(string message = "not_found") => new("not_found", message);
    public static ApiError Unauthorized(string message = "unauthorized") => new("unauthorized", message);
    public static ApiError BadRequest(string message) => new("bad_request", message);
    public static ApiError ServerError(string message) => new("server_error", message);
}
