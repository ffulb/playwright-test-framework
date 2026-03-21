using System.Net;
using System.Net.Mail;
using System.Text.Json;
using PlaywrightFramework.Config;

namespace PlaywrightFramework.Utilities;

/// <summary>
/// Sends test result notifications to leadership via email.
/// Configurable through appsettings.json notification settings.
/// </summary>
public static class NotificationService
{
    /// <summary>
    /// Sends an email notification with test results and optional report attachment.
    /// </summary>
    /// <param name="settings">Notification settings from configuration.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML content of the email body.</param>
    /// <param name="attachmentPaths">Optional file paths to attach (e.g., HTML report).</param>
    public static async Task SendEmailAsync(
        NotificationSettings settings,
        string subject,
        string htmlBody,
        params string[] attachmentPaths)
    {
        if (!settings.Enabled)
        {
            Console.WriteLine("[Notification] Email notifications disabled in config.");
            return;
        }

        if (settings.Recipients.Count == 0)
        {
            Console.WriteLine("[Notification] No recipients configured. Skipping.");
            return;
        }

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword),
            EnableSsl = settings.UseSsl
        };

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        foreach (var recipient in settings.Recipients)
        {
            message.To.Add(recipient);
        }

        foreach (var path in attachmentPaths)
        {
            if (File.Exists(path))
            {
                message.Attachments.Add(new Attachment(path));
            }
        }

        try
        {
            await client.SendMailAsync(message);
            Console.WriteLine($"[Notification] Email sent to {settings.Recipients.Count} recipient(s).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Notification] Failed to send email: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Builds an email body from test results summary.
    /// </summary>
    /// <param name="totalTests">Total test count.</param>
    /// <param name="passed">Passed test count.</param>
    /// <param name="failed">Failed test count.</param>
    /// <param name="duration">Total run duration.</param>
    /// <param name="healingCount">Number of self-healed locators.</param>
    /// <returns>HTML email body.</returns>
    public static string BuildResultsEmail(
        int totalTests,
        int passed,
        int failed,
        TimeSpan duration,
        int healingCount)
    {
        var status = failed > 0 ? "FAILED" : "PASSED";
        var statusColor = failed > 0 ? "#dc3545" : "#28a745";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; margin: 0; padding: 20px; background: #f8f9fa; }}
        .card {{ background: white; border-radius: 8px; padding: 24px; max-width: 600px; margin: 0 auto; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .status {{ font-size: 28px; font-weight: bold; color: {statusColor}; margin-bottom: 16px; }}
        .metrics {{ display: flex; gap: 16px; margin: 16px 0; }}
        .metric {{ text-align: center; padding: 12px 20px; border-radius: 6px; flex: 1; }}
        .metric-value {{ font-size: 24px; font-weight: bold; display: block; }}
        .metric-label {{ font-size: 12px; color: #666; }}
        .passed {{ background: #d4edda; }} .passed .metric-value {{ color: #155724; }}
        .failed {{ background: #f8d7da; }} .failed .metric-value {{ color: #721c24; }}
        .total {{ background: #d1ecf1; }} .total .metric-value {{ color: #0c5460; }}
        .healing {{ background: #fff3cd; }} .healing .metric-value {{ color: #856404; }}
        .footer {{ margin-top: 20px; font-size: 12px; color: #999; border-top: 1px solid #eee; padding-top: 12px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='status'>Test Run: {status}</div>
        <p>Completed at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC — Duration: {duration:mm\:ss}</p>
        <div class='metrics'>
            <div class='metric total'><span class='metric-value'>{totalTests}</span><span class='metric-label'>Total</span></div>
            <div class='metric passed'><span class='metric-value'>{passed}</span><span class='metric-label'>Passed</span></div>
            <div class='metric failed'><span class='metric-value'>{failed}</span><span class='metric-label'>Failed</span></div>
            <div class='metric healing'><span class='metric-value'>{healingCount}</span><span class='metric-label'>Healed</span></div>
        </div>
        {(failed > 0 ? "<p style='color: #dc3545;'>⚠ Some tests failed. See the attached report for details.</p>" : "<p style='color: #28a745;'>✓ All tests passed successfully.</p>")}
        <div class='footer'>
            Automated by Playwright Test Framework • See attached HTML report for full details
        </div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// Sends a complete test run notification: generates report, builds email, sends to leadership.
    /// Call this after a test run completes.
    /// </summary>
    /// <param name="settings">Test settings including notification config.</param>
    /// <param name="nunitResultsPath">Path to NUnit XML results.</param>
    /// <param name="healingReportPath">Path to healing report JSON.</param>
    public static async Task SendTestRunNotificationAsync(
        TestSettings settings,
        string nunitResultsPath,
        string healingReportPath)
    {
        // Generate HTML report
        var reportPath = Path.Combine(settings.Reporting.OutputDirectory, "test-report.html");
        await ReportGenerator.GenerateHtmlReportAsync(nunitResultsPath, healingReportPath, reportPath);

        // Count healing events
        var healingCount = 0;
        if (File.Exists(healingReportPath))
        {
            var json = await File.ReadAllTextAsync(healingReportPath);
            try
            {
                var entries = JsonSerializer.Deserialize<List<object>>(json);
                healingCount = entries?.Count ?? 0;
            }
            catch { }
        }

        // Parse basic results from NUnit XML
        int total = 0, passed = 0, failed = 0;
        var duration = TimeSpan.Zero;
        if (File.Exists(nunitResultsPath))
        {
            var xml = await File.ReadAllTextAsync(nunitResultsPath);
            total = ParseXmlAttr(xml, "total");
            passed = ParseXmlAttr(xml, "passed");
            failed = ParseXmlAttr(xml, "failed");
            var durStr = ParseXmlAttrStr(xml, "duration");
            if (double.TryParse(durStr, out var secs))
                duration = TimeSpan.FromSeconds(secs);
        }

        // Build and send email
        var subject = $"[Playwright] Test Run {(failed > 0 ? "FAILED" : "PASSED")} — {passed}/{total} passed";
        var body = BuildResultsEmail(total, passed, failed, duration, healingCount);

        await SendEmailAsync(settings.Notification, subject, body, reportPath);
    }

    private static int ParseXmlAttr(string xml, string attr)
    {
        var str = ParseXmlAttrStr(xml, attr);
        return int.TryParse(str, out var v) ? v : 0;
    }

    private static string ParseXmlAttrStr(string xml, string attr)
    {
        var pattern = $"{attr}=\"";
        var idx = xml.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "0";
        idx += pattern.Length;
        var end = xml.IndexOf('"', idx);
        return end > idx ? xml[idx..end] : "0";
    }
}
