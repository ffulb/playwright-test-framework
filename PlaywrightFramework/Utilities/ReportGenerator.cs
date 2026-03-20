using System.Text;
using System.Text.Json;

namespace PlaywrightFramework.Utilities;

/// <summary>
/// Generates HTML test reports from NUnit test results and self-healing logs.
/// Produces a human-readable summary of test execution including pass/fail
/// counts, duration, and any locators that were healed during the run.
/// </summary>
public static class ReportGenerator
{
    /// <summary>
    /// Generates an HTML report from the NUnit XML results file and healing log.
    /// </summary>
    /// <param name="nunitXmlPath">Path to the NUnit XML results file (TestResult.xml).</param>
    /// <param name="healingReportPath">Path to the self-healing JSON log.</param>
    /// <param name="outputPath">Path to write the HTML report.</param>
    public static async Task GenerateHtmlReportAsync(
        string nunitXmlPath,
        string healingReportPath,
        string outputPath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='en'>");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset='UTF-8'>");
        sb.AppendLine("  <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        sb.AppendLine("  <title>Playwright Test Report</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetReportStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class='container'>");
        sb.AppendLine("    <h1>Playwright Test Automation Report</h1>");
        sb.AppendLine($"    <p class='timestamp'>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");

        // Parse NUnit XML if available
        if (File.Exists(nunitXmlPath))
        {
            var xmlContent = await File.ReadAllTextAsync(nunitXmlPath);
            AppendTestResults(sb, xmlContent);
        }
        else
        {
            sb.AppendLine("    <div class='section'>");
            sb.AppendLine("      <h2>Test Results</h2>");
            sb.AppendLine("      <p>No NUnit results file found. Run tests with: <code>dotnet test --logger \"trx\"</code></p>");
            sb.AppendLine("    </div>");
        }

        // Parse healing report if available
        if (File.Exists(healingReportPath))
        {
            var healingJson = await File.ReadAllTextAsync(healingReportPath);
            AppendHealingReport(sb, healingJson);
        }

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(outputPath, sb.ToString());
        Console.WriteLine($"[Report] HTML report generated: {outputPath}");
    }

    /// <summary>
    /// Generates a Markdown summary of the healing report for CI/CD integration.
    /// Useful for GitHub Actions job summaries or Azure DevOps PR comments.
    /// </summary>
    /// <param name="healingReportPath">Path to the self-healing JSON log.</param>
    /// <returns>Markdown-formatted summary string.</returns>
    public static async Task<string> GenerateHealingSummaryMarkdownAsync(string healingReportPath)
    {
        if (!File.Exists(healingReportPath))
            return "No self-healing events recorded during this test run.";

        var json = await File.ReadAllTextAsync(healingReportPath);
        var records = JsonSerializer.Deserialize<List<HealingEntry>>(json) ?? new();

        if (records.Count == 0)
            return "No self-healing events recorded during this test run.";

        var sb = new StringBuilder();
        sb.AppendLine("## Self-Healing Locator Report");
        sb.AppendLine();
        sb.AppendLine($"**{records.Count} locator(s) were healed during this test run.**");
        sb.AppendLine("These locators need to be updated in the page objects to prevent future healing.");
        sb.AppendLine();
        sb.AppendLine("| Element | Original Locator | Healed To | Strategy | Page |");
        sb.AppendLine("|---------|-----------------|-----------|----------|------|");

        foreach (var r in records)
        {
            sb.AppendLine($"| {r.FriendlyName} | `{r.OriginalLocator}` | `{r.HealedLocator}` | {r.Strategy} | {r.PageUrl} |");
        }

        return sb.ToString();
    }

    #region Private helpers

    private static void AppendTestResults(StringBuilder sb, string xmlContent)
    {
        sb.AppendLine("    <div class='section'>");
        sb.AppendLine("      <h2>Test Results</h2>");

        // Simple XML parsing for key metrics (avoids System.Xml dependency)
        var total = ExtractAttribute(xmlContent, "total");
        var passed = ExtractAttribute(xmlContent, "passed");
        var failed = ExtractAttribute(xmlContent, "failed");
        var skipped = ExtractAttribute(xmlContent, "skipped");
        var duration = ExtractAttribute(xmlContent, "duration");

        sb.AppendLine("      <div class='metrics'>");
        sb.AppendLine($"        <div class='metric total'><span class='number'>{total}</span><span class='label'>Total</span></div>");
        sb.AppendLine($"        <div class='metric passed'><span class='number'>{passed}</span><span class='label'>Passed</span></div>");
        sb.AppendLine($"        <div class='metric failed'><span class='number'>{failed}</span><span class='label'>Failed</span></div>");
        sb.AppendLine($"        <div class='metric skipped'><span class='number'>{skipped}</span><span class='label'>Skipped</span></div>");
        if (!string.IsNullOrEmpty(duration))
            sb.AppendLine($"        <div class='metric duration'><span class='number'>{duration}s</span><span class='label'>Duration</span></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
    }

    private static void AppendHealingReport(StringBuilder sb, string healingJson)
    {
        var records = JsonSerializer.Deserialize<List<HealingEntry>>(healingJson) ?? new();

        sb.AppendLine("    <div class='section healing'>");
        sb.AppendLine("      <h2>Self-Healing Locator Report</h2>");

        if (records.Count == 0)
        {
            sb.AppendLine("      <p class='success'>No locators needed healing. All selectors are up to date.</p>");
        }
        else
        {
            sb.AppendLine($"      <p class='warning'><strong>{records.Count} locator(s)</strong> were healed during this run. Update these in your page objects:</p>");
            sb.AppendLine("      <table>");
            sb.AppendLine("        <thead><tr><th>Element</th><th>Original</th><th>Healed To</th><th>Strategy</th><th>Page</th></tr></thead>");
            sb.AppendLine("        <tbody>");

            foreach (var r in records)
            {
                sb.AppendLine($"        <tr><td>{Escape(r.FriendlyName)}</td><td><code>{Escape(r.OriginalLocator)}</code></td><td><code>{Escape(r.HealedLocator)}</code></td><td>{Escape(r.Strategy)}</td><td>{Escape(r.PageUrl)}</td></tr>");
            }

            sb.AppendLine("        </tbody>");
            sb.AppendLine("      </table>");
        }

        sb.AppendLine("    </div>");
    }

    private static string ExtractAttribute(string xml, string attr)
    {
        var pattern = $"{attr}=\"";
        var idx = xml.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "0";
        idx += pattern.Length;
        var end = xml.IndexOf('"', idx);
        return end > idx ? xml[idx..end] : "0";
    }

    private static string Escape(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    private static string GetReportStyles()
    {
        return @"
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f5f5f5; color: #333; line-height: 1.6; }
        .container { max-width: 1200px; margin: 0 auto; padding: 2rem; }
        h1 { color: #1a1a1a; margin-bottom: 0.5rem; }
        .timestamp { color: #666; margin-bottom: 2rem; }
        .section { background: white; border-radius: 8px; padding: 1.5rem; margin-bottom: 1.5rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
        h2 { color: #2d2d2d; margin-bottom: 1rem; border-bottom: 2px solid #eee; padding-bottom: 0.5rem; }
        .metrics { display: flex; gap: 1rem; flex-wrap: wrap; }
        .metric { text-align: center; padding: 1rem 1.5rem; border-radius: 8px; min-width: 100px; }
        .metric .number { display: block; font-size: 2rem; font-weight: bold; }
        .metric .label { display: block; font-size: 0.875rem; color: #666; }
        .total { background: #e3f2fd; }
        .passed { background: #e8f5e9; } .passed .number { color: #2e7d32; }
        .failed { background: #ffebee; } .failed .number { color: #c62828; }
        .skipped { background: #fff3e0; } .skipped .number { color: #e65100; }
        .duration { background: #f3e5f5; }
        table { width: 100%; border-collapse: collapse; margin-top: 1rem; }
        th, td { padding: 0.75rem; text-align: left; border-bottom: 1px solid #eee; }
        th { background: #fafafa; font-weight: 600; }
        code { background: #f5f5f5; padding: 0.2rem 0.4rem; border-radius: 3px; font-size: 0.875rem; }
        .warning { color: #e65100; background: #fff3e0; padding: 0.75rem; border-radius: 4px; }
        .success { color: #2e7d32; background: #e8f5e9; padding: 0.75rem; border-radius: 4px; }";
    }

    #endregion

    private sealed class HealingEntry
    {
        public DateTime Timestamp { get; set; }
        public string FriendlyName { get; set; } = "";
        public string OriginalLocator { get; set; } = "";
        public string HealedLocator { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string PageUrl { get; set; } = "";
    }
}
