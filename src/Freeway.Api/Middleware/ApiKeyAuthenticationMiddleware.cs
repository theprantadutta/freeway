using System.Security.Claims;
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
        "/health"
    };

    // Paths that are public only in development
    private static readonly string[] DevOnlyPathPrefixes = new[]
    {
        "/openapi",
        "/scalar"
    };

    public async Task InvokeAsync(HttpContext context, IProjectCacheService projectCacheService)
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

        if (string.IsNullOrEmpty(apiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { detail = "API key is required" });
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

        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { detail = "Invalid API key" });
    }
}

public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
