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
}
