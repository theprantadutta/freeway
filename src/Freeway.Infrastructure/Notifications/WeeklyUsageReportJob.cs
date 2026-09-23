using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Notifications;

public interface IWeeklyUsageReportJob
{
    Task SendWeeklyReportAsync();
}

public class WeeklyUsageReportJob : IWeeklyUsageReportJob
{
    private readonly IUsageReportBuilder _builder;
    private readonly IEmailSender _email;
    private readonly ILogger<WeeklyUsageReportJob> _logger;

    public WeeklyUsageReportJob(
        IUsageReportBuilder builder,
        IEmailSender email,
        ILogger<WeeklyUsageReportJob> logger)
    {
        _builder = builder;
        _email = email;
        _logger = logger;
    }

    public async Task SendWeeklyReportAsync()
    {
        if (!_email.IsConfigured)
        {
            _logger.LogWarning("Weekly report skipped: SMTP is not configured");
            return;
        }

        try
        {
            var report = await _builder.BuildWeeklyAsync();

            _logger.LogInformation(
                "Weekly report {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}: {Requests} requests, {Cost} USD across {Projects} projects",
                report.PeriodStart, report.PeriodEnd, report.ThisWeek.Requests,
                report.ThisWeek.CostUsd, report.Projects.Count);

            var sent = await _email.SendAsync(
                EmailTemplates.WeeklySubject(report),
                EmailTemplates.WeeklyHtml(report),
                EmailTemplates.WeeklyText(report));

            if (!sent)
                _logger.LogError("Weekly report was built but could not be sent");
        }
        catch (Exception ex)
        {
            // A reporting failure must never take the gateway down with it.
            _logger.LogError(ex, "Weekly usage report failed");
        }
    }
}
