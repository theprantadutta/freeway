using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Freeway.Api.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireProjectAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var authType = context.HttpContext.User.FindFirst("auth_type")?.Value;

        if (authType != "project")
        {
            context.Result = new UnauthorizedObjectResult(new { detail = "Project API key required" });
        }
    }
}
