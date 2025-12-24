using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Freeway.Api.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAdminAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var authType = context.HttpContext.User.FindFirst("auth_type")?.Value;

        if (authType != "admin")
        {
            context.Result = new UnauthorizedObjectResult(new { detail = "Admin access required" });
        }
    }
}
