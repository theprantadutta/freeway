using Freeway.Application.Features.Models.Queries;
using Freeway.Domain.Common;
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
    /// Selected model for a paid tier: low, moderate or premium.
    /// </summary>
    [HttpGet("/model/paid/{tier}")]
    public async Task<ActionResult> GetSelectedPaidModelForTier(string tier)
    {
        if (!PaidTierExtensions.TryParseSlug(tier, out var parsed))
            return BadRequest(new { detail = InvalidTierMessage(tier) });

        var result = await Mediator.Send(new GetSelectedPaidModelQuery(parsed));
        return HandleResult(result);
    }

    /// <summary>
    /// All models in a paid tier, ordered the way the chat fallback chain walks them:
    /// curated models first, then the rest of the tier's price band cheapest first.
    /// </summary>
    [HttpGet("/models/paid/{tier}")]
    public async Task<ActionResult> GetPaidModelsForTier(string tier)
    {
        if (!PaidTierExtensions.TryParseSlug(tier, out var parsed))
            return BadRequest(new { detail = InvalidTierMessage(tier) });

        var result = await Mediator.Send(new GetPaidModelsQuery(parsed));
        return HandleResult(result);
    }

    internal static string InvalidTierMessage(string tier) =>
        $"Unknown paid tier '{tier}'. Valid tiers are 'low', 'moderate' and 'premium'.";

    [HttpGet("/model/image")]
    public async Task<ActionResult> GetSelectedImageModel()
    {
        var result = await Mediator.Send(new GetSelectedImageModelQuery());
        return HandleResult(result);
    }

    [HttpGet("/models/image")]
    public async Task<ActionResult> GetImageModels()
    {
        var result = await Mediator.Send(new GetImageModelsQuery());
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
