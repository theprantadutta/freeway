using Freeway.Domain.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// Sends the gateway's reports over SMTP. Configured entirely from environment so
/// no credential ever reaches the repository.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;

    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly bool _useStartTls;

    public SmtpEmailSender(ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;

        _host = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.gmail.com";
        _port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
        _username = Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? "";
        _password = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? "";
        _fromAddress = Environment.GetEnvironmentVariable("SMTP_FROM") ?? _username;
        _fromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Freeway";
        _useStartTls = !bool.TryParse(Environment.GetEnvironmentVariable("SMTP_USE_STARTTLS"), out var tls) || tls;

        AdminEmail = Environment.GetEnvironmentVariable("ADMIN_NOTIFICATION_EMAIL")
                     ?? Environment.GetEnvironmentVariable("DEFAULT_ADMIN_EMAIL")
                     ?? "";
    }

    public string AdminEmail { get; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_host) &&
        !string.IsNullOrWhiteSpace(_username) &&
        !string.IsNullOrWhiteSpace(_password) &&
        !string.IsNullOrWhiteSpace(AdminEmail);

    public async Task<bool> SendAsync(
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning(
                "Email not sent: SMTP is not fully configured. Needs SMTP_USERNAME, SMTP_PASSWORD and ADMIN_NOTIFICATION_EMAIL");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromAddress));
            message.To.Add(MailboxAddress.Parse(AdminEmail));
            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = textBody
            }.ToMessageBody();

            using var client = new SmtpClient();

            // Gmail on 587 wants STARTTLS; 465 is implicit TLS.
            var socketOptions = _port == 465
                ? SecureSocketOptions.SslOnConnect
                : _useStartTls
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.Auto;

            await client.ConnectAsync(_host, _port, socketOptions, cancellationToken);
            await client.AuthenticateAsync(_username, _password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Sent \"{Subject}\" to {Recipient}", subject, AdminEmail);
            return true;
        }
        catch (Exception ex)
        {
            // Never log the password, and never let a mail failure take down a job.
            _logger.LogError(ex, "Failed to send \"{Subject}\" via {Host}:{Port}", subject, _host, _port);
            return false;
        }
    }
}
