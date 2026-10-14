using System.Security.Claims;
using Freeway.Api.Common;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Hosting;

namespace Freeway.Api.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _adminApiKey;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
        _adminApiKey = Environment.GetEnvironmentVariable("ADMIN_API_KEY") ?? "";
    }

    // Paths that never require authentication
    private static readonly string[] PublicPathPrefixes = new[]
    {
        "/health",
        "/auth/login",
        "/auth/register"
    };

    // Paths that are public only in development
    private static readonly string[] DevOnlyPathPrefixes = new[]
    {
        "/openapi",
        "/scalar"
    };

    public async Task InvokeAsync(HttpContext context, IProjectCacheService projectCacheService, IAuthService authService)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Skip authentication for root path (exact match)
        if (path == "/" || path == "")
        {
            await _next(context);
            return;
        }

        // Skip authentication for public path prefixes
        if (PublicPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Skip authentication for dev-only paths in development
        var env = context.RequestServices.GetService<IWebHostEnvironment>();
        if (env?.IsDevelopment() == true &&
            DevOnlyPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();

        // Try JWT Bearer token first
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var bearerKey = false;
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            var userId = authService.ValidateJwtToken(token);

            if (userId.HasValue)
            {
                var user = await authService.GetUserByIdAsync(userId.Value);
                if (user != null && user.IsActive)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.Name, user.Name ?? user.Email),
                        new Claim("is_admin", user.IsAdmin.ToString().ToLower()),
                        new Claim("auth_type", "user")
                    };
                    if (user.IsAdmin)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, "admin"));
                    }
                    var identity = new ClaimsIdentity(claims, "Bearer");
                    context.User = new ClaimsPrincipal(identity);
                    await _next(context);
                    return;
                }
            }

            // Not a login token, so it may be a key. Every OpenAI SDK sends its API key
            // as "Authorization: Bearer <key>" and has no notion of X-Api-Key, so this
            // is what lets one talk to the gateway with nothing but a base URL changed.
            // An explicit X-Api-Key still wins when both are sent.
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = token;
                bearerKey = true;
            }
        }

        // Try API key
        if (string.IsNullOrEmpty(apiKey))
        {
            await RejectAsync(context, "Authentication required. Provide X-Api-Key header or Bearer token.");
            return;
        }

        // Check if it's admin key
        if (apiKey == _adminApiKey)
        {
            var claims = new[]
            {
                new Claim("auth_type", "admin"),
                new Claim(ClaimTypes.Role, "admin")
            };
            var identity = new ClaimsIdentity(claims, "ApiKey");
            context.User = new ClaimsPrincipal(identity);
            await _next(context);
            return;
        }

        // Check if it's a project key
        var projectInfo = projectCacheService.ValidateApiKey(apiKey);
        if (projectInfo != null && projectInfo.IsActive)
        {
            var claims = new[]
            {
                new Claim("auth_type", "project"),
                new Claim("project_id", projectInfo.Id.ToString()),
                new Claim("project_name", projectInfo.Name),
                new Claim("rate_limit", projectInfo.RateLimitPerMinute.ToString()),
                new Claim(ClaimTypes.Role, "project")
            };
            var identity = new ClaimsIdentity(claims, "ApiKey");
            context.User = new ClaimsPrincipal(identity);
            await _next(context);
            return;
        }

        await RejectAsync(context, bearerKey ? "Invalid or expired token, or invalid API key" : "Invalid API key");
    }

    private static async Task RejectAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(ApiError.Body(StatusCodes.Status401Unauthorized, message));
    }
}

public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
