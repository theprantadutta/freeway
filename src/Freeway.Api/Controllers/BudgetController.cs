using Freeway.Api.Attributes;
using Freeway.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Freeway.Api.Controllers;

public class BudgetController : BaseApiController
{
    /// <summary>
    /// The Claude API credit as the calling project sees it: the cycle, what is left,
    /// what this project has used (this cycle and today), its cap if it has one, and
    /// what it can still spend without leaving the other projects short. The project
    /// is the one the key belongs to, the same key used for chat completions.
    /// </summary>
    [HttpGet("/budget")]
    [HttpGet("/v1/budget")]
    [RequireProject]
    public async Task<ActionResult> GetBudget([FromServices] IAnthropicCreditService credit, CancellationToken cancellationToken)
    {
        var projectId = Guid.Parse(User.FindFirst("project_id")!.Value);
        var budget = await credit.GetBudgetAsync(projectId, cancellationToken);
        return Ok(budget);
    }
}
