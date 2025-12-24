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

        return result.StatusCode switch
        {
            401 => Unauthorized(new { detail = result.Error }),
            403 => Forbid(),
            404 => NotFound(new { detail = result.Error }),
            502 => StatusCode(502, new { detail = result.Error }),
            503 => StatusCode(503, new { detail = result.Error }),
            _ => BadRequest(new { detail = result.Error })
        };
    }
}
