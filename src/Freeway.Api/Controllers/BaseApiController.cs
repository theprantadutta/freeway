using Freeway.Api.Common;
using Freeway.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Freeway.Api.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected ActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode switch
            {
                201 => StatusCode(201, result.Value),
                204 => NoContent(),
                _ => Ok(result.Value)
            };
        }

        var error = result.Error ?? "Request failed";
        return result.StatusCode switch
        {
            401 => Unauthorized(ApiError.Body(401, error)),
            403 => Forbid(),
            404 => NotFound(ApiError.Body(404, error)),
            502 => StatusCode(502, ApiError.Body(502, error)),
            503 => StatusCode(503, ApiError.Body(503, error)),
            _ => BadRequest(ApiError.Body(400, error))
        };
    }
}
