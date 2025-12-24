using System.Net.Http.Headers;
using System.Text;
using Hangfire.Dashboard;

namespace Freeway.Api.Middleware;

public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    private readonly string _username;
    private readonly string _password;

    public HangfireDashboardAuthFilter(string username, string password)
    {
        _username = username;
        _password = password;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Allow in development without auth
        var environment = httpContext.RequestServices.GetService<IWebHostEnvironment>();
        if (environment?.IsDevelopment() == true)
        {
            return true;
        }

        // Check basic auth header
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
        {
            SetUnauthorizedResponse(httpContext);
            return false;
        }

        try
        {
            var authHeaderValue = AuthenticationHeaderValue.Parse(authHeader);
            if (authHeaderValue.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
            {
                var credentials = Encoding.UTF8.GetString(
                    Convert.FromBase64String(authHeaderValue.Parameter ?? string.Empty)
                ).Split(':', 2);

                if (credentials.Length == 2 &&
                    credentials[0] == _username &&
                    credentials[1] == _password)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Invalid auth header format
        }

        SetUnauthorizedResponse(httpContext);
        return false;
    }

    private static void SetUnauthorizedResponse(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\"";
    }
}
