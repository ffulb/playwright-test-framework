using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using PlaywrightFramework.Config;
using PlaywrightFramework.Core;
using Reqnroll;

namespace PlaywrightFramework.Hooks;

/// <summary>
/// Reqnroll hooks for managing Playwright browser lifecycle.
/// Handles browser launch, context/page creation, Entra auth,
/// and artifact capture on failure.
/// </summary>
[Binding]
public sealed class PlaywrightHooks
{
    private readonly ScenarioContext _scenarioContext;

    public PlaywrightHooks(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    private static IPlaywright? _playwright;
    private static IBrowser? _browser;
    private static TestSettings? _settings;

    /// <summary>
    /// Launches browser once before all scenarios in the test run.
    /// </summary>
    [BeforeTestRun]
    public static async Task BeforeTestRun()
    {
        // Load configuration
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("Config/appsettings.json", optional: false)
            .AddJsonFile($"Config/appsettings.{Environment.GetEnvironmentVariable("TEST_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables(prefix: "TEST_");

        var configuration = configBuilder.Build();
        _settings = new TestSettings();
        configuration.GetSection("TestSettings").Bind(_settings);

        var envBaseUrl = Environment.GetEnvironmentVariable("TEST_BASE_URL");
        if (!string.IsNullOrEmpty(envBaseUrl))
            _settings.BaseUrl = envBaseUrl;

        var envHeadless = Environment.GetEnvironmentVariable("TEST_HEADLESS");
        if (!string.IsNullOrEmpty(envHeadless))
            _settings.Browser.Headless = bool.Parse(envHeadless);

        (_playwright, _browser) = await BrowserFactory.CreateAsync(_settings);
        Directory.CreateDirectory(_settings.Reporting.OutputDirectory);
    }

    /// <summary>
    /// Creates a fresh browser context and page before each scenario.
    /// Performs Entra authentication if enabled.
    /// </summary>
    [BeforeScenario(Order = 0)]
    public async Task BeforeScenario()
    {
        if (_browser == null || _settings == null)
            throw new InvalidOperationException("Browser not initialized. BeforeTestRun may have failed.");

        string? storagePath = _settings.EntraAuth.Enabled ? _settings.EntraAuth.StorageStatePath : null;
        var context = await BrowserFactory.CreateContextAsync(_browser, _settings, storagePath);

        if (_settings.Reporting.TraceOnFailure)
        {
            await context.Tracing.StartAsync(new()
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });
        }

        var page = await context.NewPageAsync();

        // Perform Entra authentication if enabled
        if (_settings.EntraAuth.Enabled)
        {
            var entra = new EntraAuth(_settings.EntraAuth);
            await entra.AuthenticateAsync(page, _settings.BaseUrl);
        }

        // Store in scenario context for step definitions to use
        _scenarioContext.Set(context);
        _scenarioContext.Set(page);
        _scenarioContext.Set(_settings);
    }

    /// <summary>
    /// Captures screenshots and traces on failure, then cleans up.
    /// </summary>
    [AfterScenario]
    public async Task AfterScenario()
    {
        var page = _scenarioContext.Get<IPage>();
        var context = _scenarioContext.Get<IBrowserContext>();
        var settings = _scenarioContext.Get<TestSettings>();
        var scenarioName = _scenarioContext.ScenarioInfo.Title.Replace(" ", "_");
        var isFailed = _scenarioContext.TestError != null;

        if (isFailed)
        {
            if (settings.Reporting.ScreenshotOnFailure)
            {
                var screenshotPath = Path.Combine(
                    settings.Reporting.OutputDirectory,
                    $"FAIL_{scenarioName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
                await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
                Console.WriteLine($"[BDD] Screenshot saved: {screenshotPath}");
            }

            if (settings.Reporting.TraceOnFailure)
            {
                var tracePath = Path.Combine(
                    settings.Reporting.OutputDirectory,
                    $"FAIL_{scenarioName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
                await context.Tracing.StopAsync(new() { Path = tracePath });
                Console.WriteLine($"[BDD] Trace saved: {tracePath}");
            }
        }
        else
        {
            if (settings.Reporting.TraceOnFailure)
            {
                await context.Tracing.StopAsync(new());
            }
        }

        await context.CloseAsync();
    }

    /// <summary>
    /// Closes browser and cleans up after all scenarios complete.
    /// </summary>
    [AfterTestRun]
    public static async Task AfterTestRun()
    {
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
