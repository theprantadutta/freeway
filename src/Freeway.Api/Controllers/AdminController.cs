using Freeway.Api.Attributes;
using Freeway.Application.DTOs;
using Freeway.Application.Features.Analytics.Queries;
using Freeway.Application.Features.Models.Commands;
using Freeway.Domain.Common;
using Freeway.Application.Features.Projects.Commands;
using Freeway.Application.Features.Projects.Queries;
using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace Freeway.Api.Controllers;

[Route("admin")]
[RequireAdmin]
public class AdminController : BaseApiController
{
    #region Projects

    [HttpGet("projects")]
    public async Task<ActionResult> GetProjects()
    {
        var result = await Mediator.Send(new GetProjectsQuery());
        return HandleResult(result);
    }

    [HttpPost("projects")]
    public async Task<ActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        var command = new CreateProjectCommand(
            Name: request.Name,
            RateLimitPerMinute: request.RateLimitPerMinute,
            Metadata: request.Metadata
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    [HttpGet("projects/{id:guid}")]
    public async Task<ActionResult> GetProject(Guid id)
    {
        var result = await Mediator.Send(new GetProjectByIdQuery(id));
        return HandleResult(result);
    }

    [HttpPatch("projects/{id:guid}")]
    public async Task<ActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request)
    {
        var command = new UpdateProjectCommand(
            Id: id,
            Name: request.Name,
            IsActive: request.IsActive,
            RateLimitPerMinute: request.RateLimitPerMinute,
            Metadata: request.Metadata
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    [HttpDelete("projects/{id:guid}")]
    public async Task<ActionResult> DeleteProject(Guid id)
    {
        var result = await Mediator.Send(new DeleteProjectCommand(id));
        return HandleResult(result);
    }

    [HttpPost("projects/{id:guid}/rotate-key")]
    public async Task<ActionResult> RotateProjectKey(Guid id)
    {
        var result = await Mediator.Send(new RotateProjectKeyCommand(id));
        return HandleResult(result);
    }

    #endregion

    [HttpPost("projects/refresh-cache")]
    public async Task<ActionResult> RefreshProjectCache([FromServices] IProjectCacheService projectCacheService)
    {
        await projectCacheService.LoadCacheAsync();
        return Ok(new { message = "Project cache refreshed successfully" });
    }

    #region Model Selection

    [HttpPut("model/free")]
    public async Task<ActionResult> SetSelectedFreeModel([FromBody] SetModelRequest request)
    {
        var result = await Mediator.Send(new SetSelectedFreeModelCommand(request.ModelId));
        return HandleResult(result);
    }

    [HttpPut("model/paid")]
    public async Task<ActionResult> SetSelectedPaidModel([FromBody] SetModelRequest request)
    {
        var result = await Mediator.Send(new SetSelectedPaidModelCommand(request.ModelId));
        return HandleResult(result);
    }

    /// <summary>
    /// Sets the selected model for one paid tier: low, moderate or premium.
    /// The model must belong to that tier.
    /// </summary>
    [HttpPut("model/paid/{tier}")]
    public async Task<ActionResult> SetSelectedPaidModelForTier(string tier, [FromBody] SetModelRequest request)
    {
        if (!PaidTierExtensions.TryParseSlug(tier, out var parsed))
            return BadRequest(new { detail = ModelsController.InvalidTierMessage(tier) });

        var result = await Mediator.Send(new SetSelectedPaidModelCommand(request.ModelId, parsed));
        return HandleResult(result);
    }

    [HttpPut("model/image")]
    public async Task<ActionResult> SetSelectedImageModel([FromBody] SetModelRequest request)
    {
        var result = await Mediator.Send(new SetSelectedImageModelCommand(request.ModelId));
        return HandleResult(result);
    }

    #endregion

    #region Analytics

    [HttpGet("analytics/summary")]
    public async Task<ActionResult> GetAnalyticsSummary()
    {
        var result = await Mediator.Send(new GetGlobalSummaryQuery());
        return HandleResult(result);
    }

    [HttpGet("analytics/usage")]
    public async Task<ActionResult> GetProjectUsage(
        [FromQuery(Name = "project_id")] Guid projectId,
        [FromQuery(Name = "start_date")] DateTime? startDate = null,
        [FromQuery(Name = "end_date")] DateTime? endDate = null)
    {
        var result = await Mediator.Send(new GetProjectUsageQuery(projectId, startDate, endDate));
        return HandleResult(result);
    }

    [HttpGet("analytics/logs")]
    public async Task<ActionResult> GetUsageLogs(
        [FromQuery(Name = "project_id")] Guid projectId,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        [FromQuery(Name = "start_date")] DateTime? startDate = null,
        [FromQuery(Name = "end_date")] DateTime? endDate = null)
    {
        var result = await Mediator.Send(new GetUsageLogsQuery(projectId, limit, offset, startDate, endDate));
        return HandleResult(result);
    }

    #endregion

    #region Notifications

    /// <summary>
    /// Sends the weekly usage report now, to the configured admin address.
    /// Same content the scheduled job produces.
    /// </summary>
    [HttpPost("notifications/weekly-report/send")]
    public async Task<ActionResult> SendWeeklyReport(
        [FromServices] IWeeklyUsageReportJob job,
        [FromServices] IEmailSender email)
    {
        if (!email.IsConfigured)
        {
            return BadRequest(new
            {
                detail = "SMTP is not configured. Set SMTP_USERNAME, SMTP_PASSWORD and ADMIN_NOTIFICATION_EMAIL."
            });
        }

        await job.SendWeeklyReportAsync();
        return Ok(new { sent = true, recipient = email.AdminEmail });
    }

    /// <summary>
    /// Previews the weekly report as JSON without sending anything.
    /// </summary>
    [HttpGet("notifications/weekly-report/preview")]
    public async Task<ActionResult> PreviewWeeklyReport([FromServices] IUsageReportBuilder builder)
    {
        var report = await builder.BuildWeeklyAsync();
        return Ok(report);
    }

    /// <summary>
    /// Runs the alert checks and returns everything currently firing, ignoring the
    /// cooldown window. Nothing is emailed.
    /// </summary>
    [HttpGet("notifications/alerts")]
    public async Task<ActionResult> GetActiveAlerts([FromServices] ISpendAlertJob job)
    {
        var alerts = await job.EvaluateAsync();
        return Ok(new { count = alerts.Count, alerts });
    }

    /// <summary>
    /// Runs the alert checks and emails anything not in cooldown.
    /// </summary>
    [HttpPost("notifications/alerts/check")]
    public async Task<ActionResult> RunAlertCheck([FromServices] ISpendAlertJob job)
    {
        await job.CheckAsync();
        return Ok(new { checked_at = DateTime.UtcNow });
    }

    #endregion
}
