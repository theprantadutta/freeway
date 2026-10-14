using System.Text.Json;
using FluentValidation;
using Freeway.Api.Common;

namespace Freeway.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            // A caller's mistake, not a fault: worth a line, not a stack trace.
            _logger.LogInformation("Request failed validation: {Errors}",
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));
            await HandleExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, detail) = exception switch
        {
            // Thrown by the validation pipeline. It used to fall through to a 500, so a
            // request with, say, no messages read as a server fault rather than a
            // mistake the caller could fix.
            ValidationException validation => (StatusCodes.Status400BadRequest,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage).Distinct())),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ArgumentException argEx => (StatusCodes.Status400BadRequest, argEx.Message),
            InvalidOperationException invEx => (StatusCodes.Status400BadRequest, invEx.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        context.Response.StatusCode = statusCode;

        var response = new
        {
            detail,
            status_code = statusCode,
            error = ApiError.Error(statusCode, detail)
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
