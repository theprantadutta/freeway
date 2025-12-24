using Freeway.Application.Features.Health.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Freeway.Api.Controllers;

public class HealthController : BaseApiController
{
    [HttpGet("/health")]
    public async Task<ActionResult> GetHealth()
    {
        var result = await Mediator.Send(new GetHealthQuery());
        return HandleResult(result);
    }

    [HttpGet("/")]
    public ActionResult GetRoot()
    {
        return Ok(new
        {
            name = "Freeway API",
            version = "1.0.0",
            description = "OpenRouter LLM proxy with project management",
            endpoints = new[]
            {
                "GET /health",
                "GET /model/free",
                "GET /model/paid",
                "GET /models/free",
                "GET /models/paid",
                "POST /chat/completions",
                "GET /admin/projects",
                "GET /admin/analytics/summary"
            }
        });
    }
}
