#pragma warning disable CA1848, CA1873
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Edith.Gateway.Services;

public interface IEmailNotificationService
{
    Task<(bool Success, string? ErrorMessage)> SendOnboardingEmailAsync(
        string toEmail,
        string fullName,
        string username,
        string temporaryPassword,
        string onboardingToken,
        Guid? tenantId = null,
        CancellationToken ct = default);
}

public sealed class SmtpEmailNotificationService : IEmailNotificationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailNotificationService> _logger;
    private readonly string? _pgConn;

    public SmtpEmailNotificationService(
        IConfiguration config,
        ILogger<SmtpEmailNotificationService> logger)
    {
        _config = config;
        _logger = logger;
        _pgConn = config.GetConnectionString("EdithPostgres");
    }

    public async Task<(bool Success, string? ErrorMessage)> SendOnboardingEmailAsync(
        string toEmail,
        string fullName,
        string username,
        string temporaryPassword,
        string onboardingToken,
        Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var smtpHost = _config["Smtp:Host"] ?? "smtp.gmail.com";
        var smtpPort = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
        var enableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) ? ssl : true;
        var smtpUser = _config["Smtp:Username"] ?? "justinkuriakose119@gmail.com";
        var smtpPass = _config["Smtp:Password"] ?? "vnmv yjxw hypz yrbl";
        var fromEmail = _config["Smtp:FromEmail"] ?? smtpUser;
        var fromName = _config["Smtp:FromName"] ?? "EDITH AML Alert Engine";
        var timeoutMs = int.TryParse(_config["Smtp:TimeoutMs"], out var t) ? t : 15000;

        var portalBaseUrl = _config["Portal:BaseUrl"] ?? "http://localhost:3000";
        var onboardingUrl = $"{portalBaseUrl}/onboarding?token={Uri.EscapeDataString(onboardingToken)}";

        var subject = "Welcome to EDITH AML Platform — Complete Your Account Activation & 2FA Setup";
        var bodyHtml = $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <style>
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; color: #0f172a; margin: 0; padding: 24px; }
                .card { max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; padding: 32px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }
                .badge { display: inline-block; padding: 4px 10px; font-size: 11px; font-weight: 700; font-family: monospace; border-radius: 6px; background: #eff6ff; color: #1d4ed8; border: 1px solid #bfdbfe; margin-bottom: 16px; }
                h1 { font-size: 20px; font-weight: 700; color: #0f172a; margin: 0 0 16px 0; }
                p { font-size: 14px; line-height: 1.6; color: #334155; margin: 0 0 16px 0; }
                .cred-box { background: #f1f5f9; border: 1px solid #cbd5e1; border-radius: 8px; padding: 16px; margin: 20px 0; }
                .cred-row { display: flex; justify-content: space-between; font-size: 13px; margin-bottom: 8px; }
                .cred-label { color: #64748b; font-weight: 500; }
                .cred-val { font-family: monospace; font-weight: 700; color: #0f172a; }
                .btn { display: block; text-align: center; background: #1e3a8a; color: #ffffff !important; text-decoration: none; font-weight: 600; font-size: 14px; padding: 12px 24px; border-radius: 8px; margin: 24px 0; }
                .footer { font-size: 12px; color: #94a3b8; text-align: center; border-top: 1px solid #e2e8f0; padding-top: 16px; margin-top: 24px; }
              </style>
            </head>
            <body>
              <div class="card">
                <span class="badge">EDITH AML // SECURITY ONBOARDING</span>
                <h1>Welcome, {{WebUtility.HtmlEncode(fullName)}}</h1>
                <p>An operator account has been provisioned for you on the <strong>EDITH Financial Crime Compliance & Real-Time AML Platform</strong>.</p>
                <p>For your security, your account requires a mandatory one-time password rotation and <strong>Two-Factor Authentication (2FA)</strong> configuration before accessing operational consoles.</p>
                
                <div class="cred-box">
                  <div class="cred-row">
                    <span class="cred-label">Username / Login ID:</span>
                    <span class="cred-val">{{WebUtility.HtmlEncode(username)}}</span>
                  </div>
                  <div class="cred-row">
                    <span class="cred-label">Temporary Auto-Generated Password:</span>
                    <span class="cred-val">{{WebUtility.HtmlEncode(temporaryPassword)}}</span>
                  </div>
                  <div class="cred-row">
                    <span class="cred-label">Activation Window:</span>
                    <span class="cred-val">48 Hours (Expires soon)</span>
                  </div>
                </div>

                <a href="{{onboardingUrl}}" class="btn" target="_blank">Activate Account & Set Up 2FA</a>

                <p style="font-size: 12px; color: #64748b;">If the button does not work, copy and paste this link into your secure browser:<br/>
                <code style="word-break: break-all; color: #1e3a8a;">{{onboardingUrl}}</code></p>

                <div class="footer">
                  This is an automated security dispatch from the EDITH Alert Engine.<br/>
                  Confidential & Proprietary • GCC Financial Crime Compliance Desks
                </div>
              </div>
            </body>
            </html>
            """;

        bool success = false;
        string? errorMessage = null;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail, fullName));

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                Timeout = timeoutMs,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            await client.SendMailAsync(message, ct);
            success = true;
            _logger.LogInformation("Successfully sent onboarding email to {Recipient} via Gmail SMTP ({Host}:{Port})", toEmail, smtpHost, smtpPort);
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to send onboarding email to {Recipient} via Gmail SMTP: {Error}", toEmail, errorMessage);
        }

        // Record dispatch attempt to database if connection available
        if (!string.IsNullOrWhiteSpace(_pgConn))
        {
            try
            {
                await using var conn = new NpgsqlConnection(_pgConn);
                await conn.OpenAsync(ct);

                const string sql = @"
                    INSERT INTO notifications.email_dispatches 
                        (id, tenant_id, recipient_email, subject, body_html, status, error_message, sent_at, created_at)
                    VALUES 
                        (@id, @tid, @rcpt, @subj, @body, @status, @err, @sentAt, NOW());";

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("tid", (object?)tenantId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("rcpt", toEmail);
                cmd.Parameters.AddWithValue("subj", subject);
                cmd.Parameters.AddWithValue("body", bodyHtml);
                cmd.Parameters.AddWithValue("status", success ? "SENT" : "FAILED");
                cmd.Parameters.AddWithValue("err", (object?)errorMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("sentAt", success ? DateTimeOffset.UtcNow : DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Could not persist email dispatch record to PostgreSQL notifications.email_dispatches");
            }
        }

        return (success, errorMessage);
    }
}
