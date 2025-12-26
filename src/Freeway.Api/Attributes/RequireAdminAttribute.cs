using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Freeway.Api.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAdminAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var authType = context.HttpContext.User.FindFirst("auth_type")?.Value;

        // Allow if authenticated with admin API key
        if (authType == "admin")
        {
            return;
        }

        // Allow if authenticated as a user with admin role
        if (authType == "user")
        {
            var isAdmin = context.HttpContext.User.FindFirst("is_admin")?.Value;
            if (isAdmin == "true")
            {
                return;
            }
        }

        context.Result = new UnauthorizedObjectResult(new { detail = "Admin access required" });
    }
}
