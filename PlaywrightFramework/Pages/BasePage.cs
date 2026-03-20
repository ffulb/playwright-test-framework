using Microsoft.Playwright;
using PlaywrightFramework.Config;
using PlaywrightFramework.Core;

namespace PlaywrightFramework.Pages;

/// <summary>
/// Base class for all Page Objects. Provides access to the Playwright page,
/// self-healing locator, and common navigation methods.
///
/// All page objects should inherit from this class to get consistent
/// self-healing behavior and configuration access.
/// </summary>
public abstract class BasePage
{
    /// <summary>The underlying Playwright page instance.</summary>
    protected IPage Page { get; }

    /// <summary>Self-healing locator for resilient element lookups.</summary>
    protected SelfHealingLocator Heal { get; }

    /// <summary>Test settings from configuration.</summary>
    protected TestSettings Settings { get; }

    /// <summary>
    /// Initializes the base page with Playwright page and configuration.
    /// </summary>
    /// <param name="page">The Playwright page instance.</param>
    /// <param name="settings">Test settings from appsettings.json.</param>
    protected BasePage(IPage page, TestSettings settings)
    {
        Page = page;
        Settings = settings;
        Heal = new SelfHealingLocator(page, settings.SelfHealing);
    }

    /// <summary>
    /// Navigates to the specified relative path under the configured BaseUrl.
    /// </summary>
    /// <param name="path">Relative URL path (e.g., "/login").</param>
    public async Task NavigateAsync(string path = "")
    {
        var url = $"{Settings.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        await Page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    /// <summary>
    /// Gets the current page title.
    /// </summary>
    public async Task<string> GetTitleAsync()
    {
        return await Page.TitleAsync();
    }

    /// <summary>
    /// Gets the current page URL.
    /// </summary>
    public string GetCurrentUrl()
    {
        return Page.Url;
    }

    /// <summary>
    /// Waits for the page to reach a network-idle state.
    /// </summary>
    public async Task WaitForLoadAsync()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Takes a screenshot and saves it to the configured output directory.
    /// </summary>
    /// <param name="name">Screenshot filename (without extension).</param>
    /// <returns>Full path to the saved screenshot.</returns>
    public async Task<string> TakeScreenshotAsync(string name)
    {
        var dir = Settings.Reporting.OutputDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        return path;
    }
}
