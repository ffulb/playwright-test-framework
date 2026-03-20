using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightFramework.Config;
using PlaywrightFramework.Core;

namespace PlaywrightFramework.Tests;

/// <summary>
/// Base class for all test fixtures. Handles browser lifecycle,
/// configuration loading, and test artifacts (screenshots, traces).
///
/// All test classes should inherit from this to get automatic
/// browser setup/teardown and configuration access.
/// </summary>
public abstract class BaseTest
{
    /// <summary>Test settings loaded from appsettings.json.</summary>
    protected TestSettings Settings { get; private set; } = null!;

    /// <summary>The Playwright instance for the current test run.</summary>
    protected IPlaywright Playwright { get; private set; } = null!;

    /// <summary>The browser instance for the current test run.</summary>
    protected IBrowser Browser { get; private set; } = null!;

    /// <summary>The browser context for the current test (isolated per test).</summary>
    protected IBrowserContext Context { get; private set; } = null!;

    /// <summary>The page instance for the current test.</summary>
    protected IPage Page { get; private set; } = null!;

    /// <summary>
    /// One-time setup for the entire test fixture. Loads config and launches the browser.
    /// </summary>
    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        // Load configuration
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("Config/appsettings.json", optional: false)
            .AddJsonFile($"Config/appsettings.{Environment.GetEnvironmentVariable("TEST_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables(prefix: "TEST_");

        var configuration = configBuilder.Build();
        Settings = new TestSettings();
        configuration.GetSection("TestSettings").Bind(Settings);

        // Allow environment variable overrides for CI/CD
        var envBaseUrl = Environment.GetEnvironmentVariable("TEST_BASE_URL");
        if (!string.IsNullOrEmpty(envBaseUrl))
            Settings.BaseUrl = envBaseUrl;

        var envHeadless = Environment.GetEnvironmentVariable("TEST_HEADLESS");
        if (!string.IsNullOrEmpty(envHeadless))
            Settings.Browser.Headless = bool.Parse(envHeadless);

        // Launch browser
        (Playwright, Browser) = await BrowserFactory.CreateAsync(Settings);

        // Ensure output directory exists
        Directory.CreateDirectory(Settings.Reporting.OutputDirectory);
    }

    /// <summary>
    /// Per-test setup. Creates an isolated browser context and page.
    /// </summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        // Use saved Entra auth state if available
        string? storagePath = Settings.EntraAuth.Enabled ? Settings.EntraAuth.StorageStatePath : null;
        Context = await BrowserFactory.CreateContextAsync(Browser, Settings, storagePath);

        // Start tracing if configured
        if (Settings.Reporting.TraceOnFailure)
        {
            await Context.Tracing.StartAsync(new()
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });
        }

        Page = await Context.NewPageAsync();
    }

    /// <summary>
    /// Per-test teardown. Captures artifacts on failure and disposes context.
    /// </summary>
    [TearDown]
    public async Task TearDownAsync()
    {
        var testName = TestContext.CurrentContext.Test.Name;
        var testResult = TestContext.CurrentContext.Result.Outcome.Status;
        var isFailed = testResult == NUnit.Framework.Interfaces.TestStatus.Failed;

        if (isFailed)
        {
            // Capture screenshot on failure
            if (Settings.Reporting.ScreenshotOnFailure)
            {
                var screenshotPath = Path.Combine(
                    Settings.Reporting.OutputDirectory,
                    $"FAIL_{testName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
                await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
                TestContext.AddTestAttachment(screenshotPath, "Failure Screenshot");
                Console.WriteLine($"[Report] Screenshot saved: {screenshotPath}");
            }

            // Save trace on failure
            if (Settings.Reporting.TraceOnFailure)
            {
                var tracePath = Path.Combine(
                    Settings.Reporting.OutputDirectory,
                    $"FAIL_{testName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
                await Context.Tracing.StopAsync(new() { Path = tracePath });
                TestContext.AddTestAttachment(tracePath, "Failure Trace");
                Console.WriteLine($"[Report] Trace saved: {tracePath}");
            }
        }
        else
        {
            // Discard trace on success
            if (Settings.Reporting.TraceOnFailure)
            {
                await Context.Tracing.StopAsync(new());
            }
        }

        await Context.CloseAsync();
    }

    /// <summary>
    /// One-time teardown for the entire test fixture. Closes browser and saves healing report.
    /// </summary>
    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        // Save self-healing report
        await SelfHealingLocator.SaveHealingReportAsync(Settings.SelfHealing.HealingReportPath);

        await Browser.CloseAsync();
        Playwright.Dispose();
    }
}
