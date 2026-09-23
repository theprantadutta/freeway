using System.Globalization;
using System.Net;
using System.Text;
using Freeway.Domain.Interfaces;

namespace Freeway.Infrastructure.Notifications;

/// <summary>
/// Renders the report and alert emails.
///
/// Mail clients strip stylesheets and mostly ignore flexbox, so this is tables and
/// inline styles throughout, and light-mode only. Lane colours match the control
/// panel so a tier looks the same in the inbox as it does on screen.
/// </summary>
public static class EmailTemplates
{
    private const string Ink = "#101520";
    private const string Muted = "#586274";
    private const string Subtle = "#828C9E";
    private const string Line = "#E3E7EE";
    private const string Surface = "#F7F8FB";
    private const string Brand = "#4F46E5";

    private static readonly Dictionary<string, string> TierColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["free"] = "#059669",
        ["low"] = "#0284C7",
        ["moderate"] = "#D97706",
        ["premium"] = "#E11D48",
        ["image"] = "#7C3AED",
        ["other"] = "#828C9E"
    };

    private static string TierColor(string? tier) =>
        tier != null && TierColors.TryGetValue(tier, out var c) ? c : Brand;

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string Money(decimal value)
    {
        if (value == 0) return "$0.00";
        if (value < 0.01m) return "$" + value.ToString("0.000000", CultureInfo.InvariantCulture);
        if (value < 1m) return "$" + value.ToString("0.0000", CultureInfo.InvariantCulture);
        return "$" + value.ToString("N2", CultureInfo.InvariantCulture);
    }

    private static string Num(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Day(DateTime value) => value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    // -----------------------------------------------------------------------
    // Weekly report
    // -----------------------------------------------------------------------

    public static string WeeklySubject(WeeklyUsageReport r) =>
        r.HadTraffic
            ? $"Freeway · {Money(r.ThisWeek.CostUsd)} across {Num(r.ThisWeek.Requests)} requests ({Day(r.PeriodStart)}–{Day(r.PeriodEnd)})"
            : $"Freeway · no traffic {Day(r.PeriodStart)}–{Day(r.PeriodEnd)}";

    public static string WeeklyHtml(WeeklyUsageReport r)
    {
        var sb = new StringBuilder();
        sb.Append(HeadOpen("Weekly usage", $"{Day(r.PeriodStart)} – {Day(r.PeriodEnd)}"));

        // Headline figures
        sb.Append($@"
<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px'>
  <tr>
    {MetricCell("Spent this week", Money(r.ThisWeek.CostUsd), ChangeNote(r))}
    {MetricCell("Requests", Num(r.ThisWeek.Requests), $"{r.ThisWeek.SuccessRate:0.0}% succeeded")}
  </tr>
  <tr>
    {MetricCell("Month to date", Money(r.MonthToDate.CostUsd), $"{Num(r.MonthToDate.Requests)} requests")}
    {MetricCell("All time", Money(r.AllTime.CostUsd), $"{Num(r.AllTime.Requests)} requests")}
  </tr>
</table>");

        if (!r.HadTraffic)
        {
            sb.Append($@"
<p style='margin:0 0 24px;padding:14px 16px;background:{Surface};border:1px solid {Line};border-radius:8px;color:{Muted};font-size:14px;line-height:1.5'>
  Nothing ran through the gateway in this period. You are seeing this email so you know the
  reporting job itself is still alive.
</p>");
            sb.Append(CreditBlock(r.Credit));
            sb.Append(FootClose(r));
            return sb.ToString();
        }

        // Highlights. Cost-based lines only earn their place when something was billed;
        // on an all-free week they would just repeat "$0.00" three times.
        var billed = r.ThisWeek.CostUsd > 0;

        sb.Append(SectionTitle("Highlights"));
        sb.Append("<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px'>");

        if (r.MostUsedModel is { } used)
            sb.Append(HighlightRow("Most used model", used.ModelId,
                billed
                    ? $"{Num(used.Requests)} requests · {Money(used.CostUsd)}"
                    : $"{Num(used.Requests)} requests · free",
                TierColor(used.ModelTier ?? used.ModelType)));

        if (billed)
        {
            if (r.MostExpensiveModel is { CostUsd: > 0 } exp)
                sb.Append(HighlightRow("Cost the most", exp.ModelId,
                    $"{Money(exp.CostUsd)} · {Money(exp.CostPerRequest)} per request", TierColor(exp.ModelTier ?? exp.ModelType)));
            if (r.BestValueModel is { } cheap && r.MostExpensiveModel?.ModelId != cheap.ModelId)
                sb.Append(HighlightRow("Cheapest per request", cheap.ModelId,
                    $"{Money(cheap.CostPerRequest)} per request · {Money(cheap.CostUsd)} total", TierColor(cheap.ModelTier ?? cheap.ModelType)));
            if (r.TopProject is { CostUsd: > 0 } top)
                sb.Append(HighlightRow("Biggest spender", top.ProjectName,
                    $"{Money(top.CostUsd)} · {Num(top.Requests)} requests", Brand));
        }
        else if (r.TopProject is { } busiest)
        {
            sb.Append(HighlightRow("Busiest project", busiest.ProjectName,
                $"{Num(busiest.Requests)} requests", Brand));
        }

        sb.Append("</table>");

        if (!billed)
        {
            sb.Append($@"
<p style='margin:-8px 0 24px;padding:12px 14px;background:#0596690F;border:1px solid #05966933;border-radius:8px;color:{Muted};font-size:13px;line-height:1.5'>
  Everything this week ran on the free lane, so nothing was billed.
</p>");
        }

        // Per project
        if (r.Projects.Count > 0)
        {
            sb.Append(SectionTitle("By project"));
            sb.Append(TableOpen("Project", "Requests", "Tokens", "Cost"));
            foreach (var p in r.Projects)
            {
                var failed = p.FailedRequests > 0
                    ? $"<div style='color:#E11D48;font-size:12px;margin-top:2px'>{Num(p.FailedRequests)} failed</div>"
                    : "";
                sb.Append($@"<tr>
  <td style='{TdLeft}'>{E(p.ProjectName)}{failed}</td>
  <td style='{TdRight}'>{Num(p.Requests)}</td>
  <td style='{TdRight}'>{Num(p.Tokens)}</td>
  <td style='{TdRight}'><strong>{Money(p.CostUsd)}</strong></td>
</tr>");
            }
            sb.Append("</table>");
        }

        // Per lane
        if (r.Tiers.Count > 0)
        {
            sb.Append(SectionTitle("By lane"));
            sb.Append(TableOpen("Lane", "Requests", "", "Cost"));
            foreach (var t in r.Tiers)
            {
                sb.Append($@"<tr>
  <td style='{TdLeft}'>
    <span style='display:inline-block;width:8px;height:8px;border-radius:8px;background:{TierColor(t.Tier)};margin-right:8px'></span>{E(t.Tier)}
  </td>
  <td style='{TdRight}'>{Num(t.Requests)}</td>
  <td style='{TdRight}'></td>
  <td style='{TdRight}'><strong>{Money(t.CostUsd)}</strong></td>
</tr>");
            }
            sb.Append("</table>");
        }

        // Per model
        if (r.Models.Count > 0)
        {
            sb.Append(SectionTitle("By model"));
            sb.Append(TableOpen("Model", "Requests", "Per request", "Cost"));
            foreach (var m in r.Models.Take(15))
            {
                sb.Append($@"<tr>
  <td style='{TdLeft}'>
    <span style='display:inline-block;width:8px;height:8px;border-radius:8px;background:{TierColor(m.ModelTier ?? m.ModelType)};margin-right:8px'></span>
    <span style='font-family:ui-monospace,Menlo,Consolas,monospace;font-size:13px'>{E(m.ModelId)}</span>
  </td>
  <td style='{TdRight}'>{Num(m.Requests)}</td>
  <td style='{TdRight}'>{Money(m.CostPerRequest)}</td>
  <td style='{TdRight}'><strong>{Money(m.CostUsd)}</strong></td>
</tr>");
            }
            sb.Append("</table>");
            if (r.Models.Count > 15)
                sb.Append($"<p style='margin:8px 0 0;color:{Subtle};font-size:12px'>and {r.Models.Count - 15} more</p>");
        }

        sb.Append(CreditBlock(r.Credit));
        sb.Append(FootClose(r));
        return sb.ToString();
    }

    public static string WeeklyText(WeeklyUsageReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"FREEWAY — WEEKLY USAGE");
        sb.AppendLine($"{Day(r.PeriodStart)} to {Day(r.PeriodEnd)}");
        sb.AppendLine(new string('-', 52));
        sb.AppendLine();

        if (!r.HadTraffic)
        {
            sb.AppendLine("No requests ran through the gateway this period.");
            sb.AppendLine();
            sb.AppendLine($"All time cost : {Money(r.AllTime.CostUsd)}");
            sb.AppendLine($"Projects      : {r.ActiveProjects} active of {r.TotalProjects}");
            AppendCreditText(sb, r.Credit);
            return sb.ToString();
        }

        sb.AppendLine($"This week     : {Money(r.ThisWeek.CostUsd)}  ({Num(r.ThisWeek.Requests)} requests)");
        sb.AppendLine($"Previous week : {Money(r.PreviousWeek.CostUsd)}  ({Num(r.PreviousWeek.Requests)} requests)");
        if (r.CostChangePercent is { } change)
            sb.AppendLine($"Change        : {(change >= 0 ? "+" : "")}{change:0.0}%");
        sb.AppendLine($"Month to date : {Money(r.MonthToDate.CostUsd)}");
        sb.AppendLine($"All time      : {Money(r.AllTime.CostUsd)}");
        sb.AppendLine($"Success rate  : {r.ThisWeek.SuccessRate:0.0}%  ({Num(r.ThisWeek.FailedRequests)} failed)");
        sb.AppendLine($"Tokens        : {Num(r.ThisWeek.TotalTokens)}  ({Num(r.ThisWeek.InputTokens)} in / {Num(r.ThisWeek.OutputTokens)} out)");
        sb.AppendLine();

        if (r.MostUsedModel is { } used)
            sb.AppendLine($"Most used     : {used.ModelId} ({Num(used.Requests)} requests)");
        if (r.ThisWeek.CostUsd > 0)
        {
            if (r.MostExpensiveModel is { CostUsd: > 0 } exp)
                sb.AppendLine($"Cost the most : {exp.ModelId} ({Money(exp.CostUsd)})");
            if (r.BestValueModel is { } cheap)
                sb.AppendLine($"Cheapest/req  : {cheap.ModelId} ({Money(cheap.CostPerRequest)})");
        }
        else
        {
            sb.AppendLine("Billing       : nothing billed, all traffic was on the free lane");
        }
        sb.AppendLine();

        if (r.Projects.Count > 0)
        {
            sb.AppendLine("BY PROJECT");
            foreach (var p in r.Projects)
                sb.AppendLine($"  {p.ProjectName,-28} {Num(p.Requests),8} req  {Money(p.CostUsd),12}");
            sb.AppendLine();
        }

        if (r.Tiers.Count > 0)
        {
            sb.AppendLine("BY LANE");
            foreach (var t in r.Tiers)
                sb.AppendLine($"  {t.Tier,-28} {Num(t.Requests),8} req  {Money(t.CostUsd),12}");
            sb.AppendLine();
        }

        if (r.Models.Count > 0)
        {
            sb.AppendLine("BY MODEL");
            foreach (var m in r.Models.Take(15))
                sb.AppendLine($"  {m.ModelId,-40} {Num(m.Requests),7} req  {Money(m.CostUsd),12}");
            sb.AppendLine();
        }

        AppendCreditText(sb, r.Credit);
        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Alerts
    // -----------------------------------------------------------------------

    public static string AlertSubject(List<SpendAlert> alerts)
    {
        if (alerts.Count == 1) return $"Freeway alert · {alerts[0].Title}";
        return $"Freeway · {alerts.Count} alerts";
    }

    public static string AlertHtml(List<SpendAlert> alerts)
    {
        var worst = alerts.Max(a => a.Severity);
        var sb = new StringBuilder();
        sb.Append(HeadOpen(
            worst == AlertSeverity.Critical ? "Something needs attention" : "Heads up",
            DateTime.UtcNow.ToString("d MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)));

        foreach (var a in alerts)
        {
            var color = a.Severity switch
            {
                AlertSeverity.Critical => "#E11D48",
                AlertSeverity.Warning => "#D97706",
                _ => "#0284C7"
            };

            sb.Append($@"
<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 12px'>
  <tr>
    <td style='padding:14px 16px;background:{Surface};border:1px solid {Line};border-left:3px solid {color};border-radius:8px'>
      <div style='font-size:15px;font-weight:600;color:{Ink}'>{E(a.Title)}</div>
      <div style='margin-top:4px;font-size:14px;color:{Muted};line-height:1.5'>{E(a.Detail)}</div>
      {(a.Action is null ? "" : $"<div style='margin-top:8px;font-size:13px;color:{Ink}'><strong>What to do:</strong> {E(a.Action)}</div>")}
    </td>
  </tr>
</table>");
        }

        sb.Append($@"
<p style='margin:20px 0 0;color:{Subtle};font-size:12px;line-height:1.5'>
  Each alert stays quiet for a cooldown period after firing, so you get one message rather than one per check.
</p>");
        sb.Append(Close());
        return sb.ToString();
    }

    public static string AlertText(List<SpendAlert> alerts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FREEWAY — ALERT");
        sb.AppendLine(DateTime.UtcNow.ToString("d MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture));
        sb.AppendLine(new string('-', 52));
        sb.AppendLine();
        foreach (var a in alerts)
        {
            sb.AppendLine($"[{a.Severity.ToString().ToUpperInvariant()}] {a.Title}");
            sb.AppendLine($"  {a.Detail}");
            if (a.Action is not null) sb.AppendLine($"  What to do: {a.Action}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Shared chrome
    // -----------------------------------------------------------------------

    private const string TdLeft =
        "padding:10px 12px;border-top:1px solid #E3E7EE;font-size:14px;color:#101520;text-align:left";

    private const string TdRight =
        "padding:10px 12px;border-top:1px solid #E3E7EE;font-size:14px;color:#101520;text-align:right;white-space:nowrap";

    private static string HeadOpen(string title, string subtitle) => $@"<!doctype html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'></head>
<body style='margin:0;padding:0;background:{Surface}'>
<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background:{Surface};padding:24px 12px'>
<tr><td align='center'>
<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='max-width:640px;background:#FFFFFF;border:1px solid {Line};border-radius:12px;padding:28px;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif'>
<tr><td>
  <table role='presentation' cellpadding='0' cellspacing='0' style='margin:0 0 6px;border-collapse:collapse'>
    <tr>
      <td valign='middle' style='padding-right:10px'>
        <table role='presentation' cellpadding='0' cellspacing='0' style='border-collapse:collapse'>
          {LaneBar("#059669", 18)}
          {LaneBar("#0284C7", 14)}
          {LaneBar("#D97706", 10)}
          {LaneBar("#E11D48", 6)}
        </table>
      </td>
      <td valign='middle' style='font-size:15px;font-weight:600;color:{Ink};line-height:1'>Freeway</td>
    </tr>
  </table>
  <h1 style='margin:12px 0 2px;font-size:22px;font-weight:600;color:{Ink};letter-spacing:-0.02em'>{E(title)}</h1>
  <p style='margin:0 0 24px;font-size:14px;color:{Muted}'>{E(subtitle)}</p>";

    private static string Close() => @"
</td></tr></table>
</td></tr></table>
</body></html>";

    private static string FootClose(WeeklyUsageReport r) => $@"
<p style='margin:24px 0 0;padding-top:16px;border-top:1px solid {Line};color:{Subtle};font-size:12px;line-height:1.6'>
  {r.ActiveProjects} active of {r.TotalProjects} projects ·
  avg response {r.ThisWeek.AvgResponseTimeMs:0}ms ·
  {Num(r.ThisWeek.TotalTokens)} tokens this week<br>
  Sent by your Freeway gateway. Change the schedule with WEEKLY_REPORT_CRON, or turn it off with WEEKLY_REPORT_ENABLED=false.
</p>" + Close();

    /// <summary>One lane of the mark. Height lives on the cell so Outlook keeps it.</summary>
    private static string LaneBar(string color, int width) => $@"
<tr><td style='height:3px;line-height:3px;font-size:0;padding:0 0 2px'>
  <div style='width:{width}px;height:3px;background:{color};border-radius:2px;font-size:0;line-height:3px'>&nbsp;</div>
</td></tr>";

    private static string SectionTitle(string text) =>
        $"<h2 style='margin:24px 0 8px;font-size:15px;font-weight:600;color:{Ink}'>{E(text)}</h2>";

    private static string MetricCell(string label, string value, string? note) => $@"
<td width='50%' style='padding:14px 16px;border:1px solid {Line};border-radius:8px'>
  <div style='font-size:13px;color:{Muted}'>{E(label)}</div>
  <div style='margin-top:4px;font-size:24px;font-weight:600;color:{Ink};letter-spacing:-0.02em'>{E(value)}</div>
  {(string.IsNullOrEmpty(note) ? "" : $"<div style='margin-top:2px;font-size:12px;color:{Subtle}'>{note}</div>")}
</td>";

    private static string ChangeNote(WeeklyUsageReport r)
    {
        if (r.CostChangePercent is not { } change)
            return $"vs {Money(r.PreviousWeek.CostUsd)} last week";

        var color = change > 0 ? "#E11D48" : "#059669";
        var arrow = change > 0 ? "&#9650;" : "&#9660;";
        return $"<span style='color:{color}'>{arrow} {Math.Abs(change):0.0}%</span> vs last week";
    }

    private static string HighlightRow(string label, string value, string note, string color) => $@"
<tr>
  <td style='padding:10px 0;border-top:1px solid {Line}'>
    <div style='font-size:12px;color:{Subtle}'>{E(label)}</div>
    <div style='margin-top:3px;font-size:14px;color:{Ink}'>
      <span style='display:inline-block;width:8px;height:8px;border-radius:8px;background:{color};margin-right:8px'></span>
      <span style='font-family:ui-monospace,Menlo,Consolas,monospace'>{E(value)}</span>
    </div>
    <div style='margin-top:2px;font-size:12px;color:{Muted}'>{E(note)}</div>
  </td>
</tr>";

    private static string TableOpen(string c1, string c2, string c3, string c4) => $@"
<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse'>
<tr>
  <th style='padding:0 12px 8px;font-size:12px;font-weight:500;color:{Subtle};text-align:left'>{E(c1)}</th>
  <th style='padding:0 12px 8px;font-size:12px;font-weight:500;color:{Subtle};text-align:right'>{E(c2)}</th>
  <th style='padding:0 12px 8px;font-size:12px;font-weight:500;color:{Subtle};text-align:right'>{E(c3)}</th>
  <th style='padding:0 12px 8px;font-size:12px;font-weight:500;color:{Subtle};text-align:right'>{E(c4)}</th>
</tr>";

    private static string CreditBlock(OpenRouterCredit? credit)
    {
        if (credit is null) return "";

        var low = credit.Remaining < 2m;
        var color = low ? "#E11D48" : Ink;
        var keyLine = credit.KeyLimit.HasValue
            ? $"<div style='margin-top:4px;font-size:12px;color:{Muted}'>Key limit {Money(credit.KeyLimitRemaining ?? 0)} of {Money(credit.KeyLimit.Value)} remaining</div>"
            : "";
        var expiryLine = credit.KeyExpiresAt.HasValue
            ? $"<div style='margin-top:2px;font-size:12px;color:{Muted}'>Key expires {Day(credit.KeyExpiresAt.Value)}</div>"
            : "";

        return $@"
{SectionTitle("OpenRouter credit")}
<table role='presentation' width='100%' cellpadding='0' cellspacing='0'>
  <tr><td style='padding:14px 16px;background:{Surface};border:1px solid {Line};border-radius:8px'>
    <div style='font-size:13px;color:{Muted}'>Remaining balance</div>
    <div style='margin-top:4px;font-size:20px;font-weight:600;color:{color}'>{Money(credit.Remaining)}</div>
    <div style='margin-top:2px;font-size:12px;color:{Subtle}'>{Money(credit.TotalUsage)} used of {Money(credit.TotalCredits)}</div>
    {keyLine}
    {expiryLine}
  </td></tr>
</table>";
    }

    private static void AppendCreditText(StringBuilder sb, OpenRouterCredit? credit)
    {
        if (credit is null) return;
        sb.AppendLine("OPENROUTER CREDIT");
        sb.AppendLine($"  Remaining : {Money(credit.Remaining)} (used {Money(credit.TotalUsage)} of {Money(credit.TotalCredits)})");
        if (credit.KeyLimit.HasValue)
            sb.AppendLine($"  Key limit : {Money(credit.KeyLimitRemaining ?? 0)} of {Money(credit.KeyLimit.Value)} remaining");
        if (credit.KeyExpiresAt.HasValue)
            sb.AppendLine($"  Key expires: {Day(credit.KeyExpiresAt.Value)}");
    }
}
