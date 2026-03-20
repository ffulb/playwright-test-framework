using Microsoft.Playwright;
using PlaywrightFramework.Config;

namespace PlaywrightFramework.Core;

/// <summary>
/// Creates and configures Playwright browser instances based on appsettings.json.
/// Supports Chromium, Firefox, and WebKit engines.
/// </summary>
public static class BrowserFactory
{
    /// <summary>
    /// Creates a new Playwright instance and launches the configured browser.
    /// </summary>
    /// <param name="settings">Test settings from configuration.</param>
    /// <returns>A tuple of the Playwright instance and launched browser.</returns>
    public static async Task<(IPlaywright Playwright, IBrowser Browser)> CreateAsync(TestSettings settings)
    {
        var playwright = await Playwright.CreateAsync();

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = settings.Browser.Headless,
            SlowMo = settings.Browser.SlowMo
        };

        IBrowser browser = settings.Browser.Type.ToLowerInvariant() switch
        {
            "firefox" => await playwright.Firefox.LaunchAsync(launchOptions),
            "webkit" => await playwright.Webkit.LaunchAsync(launchOptions),
            _ => await playwright.Chromium.LaunchAsync(launchOptions)
        };

        return (playwright, browser);
    }

    /// <summary>
    /// Creates a new browser context with configured viewport and timeout.
    /// </summary>
    /// <param name="browser">The browser instance.</param>
    /// <param name="settings">Test settings from configuration.</param>
    /// <param name="storageStatePath">Optional path to saved storage state for session reuse.</param>
    /// <returns>A configured browser context.</returns>
    public static async Task<IBrowserContext> CreateContextAsync(
        IBrowser browser,
        TestSettings settings,
        string? storageStatePath = null)
    {
        var contextOptions = new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = settings.Browser.ViewportWidth,
                Height = settings.Browser.ViewportHeight
            },
            BaseURL = settings.BaseUrl
        };

        // Reuse saved authentication state if available
        if (!string.IsNullOrEmpty(storageStatePath) && File.Exists(storageStatePath))
        {
            contextOptions.StorageStatePath = storageStatePath;
        }

        var context = await browser.NewContextAsync(contextOptions);
        context.SetDefaultTimeout(settings.Browser.Timeout);

        return context;
    }
}
