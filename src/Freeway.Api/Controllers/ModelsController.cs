using Freeway.Application.Features.Models.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Freeway.Api.Controllers;

public class ModelsController : BaseApiController
{
    [HttpGet("/model/free")]
    public async Task<ActionResult> GetSelectedFreeModel()
    {
        var result = await Mediator.Send(new GetSelectedFreeModelQuery());
        return HandleResult(result);
    }

    [HttpGet("/model/paid")]
    public async Task<ActionResult> GetSelectedPaidModel()
    {
        var result = await Mediator.Send(new GetSelectedPaidModelQuery());
        return HandleResult(result);
    }

    [HttpGet("/models/free")]
    public async Task<ActionResult> GetFreeModels()
    {
        var result = await Mediator.Send(new GetFreeModelsQuery());
        return HandleResult(result);
    }

    [HttpGet("/models/paid")]
    public async Task<ActionResult> GetPaidModels()
    {
        var result = await Mediator.Send(new GetPaidModelsQuery());
        return HandleResult(result);
    }

    /// <summary>
    /// Get all models from all providers
    /// </summary>
    [HttpGet("/v1/models")]
    public async Task<ActionResult> GetAllProviderModels([FromQuery] string? provider = null)
    {
        var result = await Mediator.Send(new GetProviderModelsQuery(provider));
        return HandleResult(result);
    }

    /// <summary>
    /// Get list of available providers with status
    /// </summary>
    [HttpGet("/v1/providers")]
    public async Task<ActionResult> GetProviders()
    {
        var result = await Mediator.Send(new GetProvidersQuery());
        return HandleResult(result);
    }
}
