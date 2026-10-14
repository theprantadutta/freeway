namespace Freeway.Api.Common;

/// <summary>
/// The body every error response carries.
///
/// Two shapes in one: "detail", which this API has always sent and the control panel
/// reads, and "error", the object OpenAI clients look for. The SDKs take an error's
/// message and type from there, so without it a caller using one gets the raw body
/// dumped into an exception instead of the reason.
/// </summary>
public static class ApiError
{
    public static object Body(int statusCode, string message) => new
    {
        detail = message,
        error = Error(statusCode, message)
    };

    /// <summary>Just the OpenAI error object, for a failure sent mid-stream.</summary>
    public static object Error(int? statusCode, string message) => new
    {
        message,
        type = TypeFor(statusCode),
        param = (string?)null,
        code = statusCode
    };

    private static string TypeFor(int? statusCode) => statusCode switch
    {
        400 or 413 or 422 => "invalid_request_error",
        401 => "authentication_error",
        403 => "permission_error",
        404 => "not_found_error",
        429 => "rate_limit_error",
        _ => "api_error"
    };
}
