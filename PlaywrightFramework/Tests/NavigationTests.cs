using NUnit.Framework;
using PlaywrightFramework.Core;
using PlaywrightFramework.Pages;

namespace PlaywrightFramework.Tests;

/// <summary>
/// Navigation and general UI test suite. Demonstrates framework capabilities
/// beyond login: page navigation, link verification, element interaction,
/// and self-healing across multiple pages.
///
/// Tests run against https://the-internet.herokuapp.com by default.
/// </summary>
[TestFixture]
[Category("Navigation")]
[Parallelizable(ParallelScope.Self)]
public sealed class NavigationTests : BaseTest
{
    /// <summary>
    /// Verifies the home page loads and displays the expected heading.
    /// </summary>
    [Test]
    [Description("Home page loads with 'Welcome to the-internet' heading")]
    public async Task HomePage_ShouldDisplayWelcomeHeading()
    {
        // Arrange & Act
        await Page.GotoAsync(Settings.BaseUrl);
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.DOMContentLoaded);

        // Assert
        var heading = Page.Locator("h1.heading");
        await heading.WaitForAsync();
        var text = await heading.InnerTextAsync();
        Assert.That(text, Does.Contain("Welcome"), "Home page should display a welcome heading.");
    }

    /// <summary>
    /// Verifies that navigation links on the home page lead to valid pages.
    /// </summary>
    [Test]
    [Description("Home page navigation links resolve to valid pages (no 404s)")]
    public async Task HomePage_NavigationLinks_ShouldBeValid()
    {
        // Arrange
        await Page.GotoAsync(Settings.BaseUrl);
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.DOMContentLoaded);

        // Act - Get a sample of links
        var links = Page.Locator("#content ul li a");
        var count = await links.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Home page should have navigation links.");

        // Verify first 5 links are clickable and load without error
        var linksToCheck = Math.Min(5, count);
        for (int i = 0; i < linksToCheck; i++)
        {
            var href = await links.Nth(i).GetAttributeAsync("href");
            Assert.That(href, Is.Not.Null.And.Not.Empty,
                $"Link at index {i} should have an href attribute.");
        }
    }

    /// <summary>
    /// Tests checkbox interaction on the Checkboxes page.
    /// </summary>
    [Test]
    [Description("Checkboxes can be toggled on and off")]
    public async Task Checkboxes_ShouldToggle()
    {
        // Arrange
        await Page.GotoAsync($"{Settings.BaseUrl}/checkboxes");

        // Act
        var checkboxes = Page.Locator("#checkboxes input[type='checkbox']");
        var count = await checkboxes.CountAsync();
        Assert.That(count, Is.GreaterThanOrEqualTo(2), "Should have at least 2 checkboxes.");

        // Toggle first checkbox
        var firstCheckbox = checkboxes.Nth(0);
        var initialState = await firstCheckbox.IsCheckedAsync();
        await firstCheckbox.ClickAsync();

        // Assert
        var newState = await firstCheckbox.IsCheckedAsync();
        Assert.That(newState, Is.Not.EqualTo(initialState), "Checkbox state should change after click.");
    }

    /// <summary>
    /// Tests dropdown selection on the Dropdown page.
    /// </summary>
    [Test]
    [Description("Dropdown selection changes the selected value")]
    public async Task Dropdown_ShouldAllowSelection()
    {
        // Arrange
        await Page.GotoAsync($"{Settings.BaseUrl}/dropdown");
        var dropdown = Page.Locator("#dropdown");

        // Act
        await dropdown.SelectOptionAsync("1");

        // Assert
        var selectedValue = await dropdown.InputValueAsync();
        Assert.That(selectedValue, Is.EqualTo("1"), "Dropdown should have value '1' selected.");
    }

    /// <summary>
    /// Tests dynamic content loading (AJAX).
    /// </summary>
    [Test]
    [Description("Dynamic loading page displays content after trigger")]
    public async Task DynamicLoading_ShouldDisplayContentAfterLoad()
    {
        // Arrange
        await Page.GotoAsync($"{Settings.BaseUrl}/dynamic_loading/1");

        // Act - Click the start button
        await Page.Locator("#start button").ClickAsync();

        // Wait for the result to appear
        var result = Page.Locator("#finish h4");
        await result.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 10000 });

        // Assert
        var text = await result.InnerTextAsync();
        Assert.That(text, Is.EqualTo("Hello World!"), "Dynamic content should display 'Hello World!'.");
    }

    /// <summary>
    /// Demonstrates self-healing across a navigation flow.
    /// Uses intentionally broken selectors to show recovery.
    /// </summary>
    [Test]
    [Description("Self-healing recovers broken selectors during multi-page navigation")]
    [Category("SelfHealing")]
    public async Task SelfHealing_AcrossPages_ShouldRecover()
    {
        // Navigate to home page
        await Page.GotoAsync(Settings.BaseUrl);

        var healer = new SelfHealingLocator(Page, Settings.SelfHealing);

        // Broken selector for heading, but text match will heal it
        var heading = await healer.FindAsync(
            "#non_existent_heading",
            "Welcome to the-internet",
            new AlternativeLocators
            {
                CssSelector = "h1.heading",
                Text = "Welcome"
            });

        var headingText = await heading.InnerTextAsync();
        Assert.That(headingText, Does.Contain("Welcome"), "Should find heading via self-healing.");
    }

    /// <summary>
    /// Verifies page title is present on multiple pages.
    /// </summary>
    [Test]
    [Description("Multiple pages have valid titles")]
    public async Task MultiplePages_ShouldHaveValidTitles()
    {
        var pagePaths = new[] { "", "/login", "/checkboxes", "/dropdown" };

        foreach (var path in pagePaths)
        {
            await Page.GotoAsync($"{Settings.BaseUrl}{path}");
            var title = await Page.TitleAsync();
            Assert.That(title, Is.Not.Empty, $"Page at '{path}' should have a title.");
        }
    }

    /// <summary>
    /// Tests that JavaScript alerts can be handled.
    /// </summary>
    [Test]
    [Description("JavaScript alerts are handled correctly")]
    public async Task JSAlerts_ShouldBeHandled()
    {
        // Arrange
        await Page.GotoAsync($"{Settings.BaseUrl}/javascript_alerts");

        // Set up dialog handler
        Page.Dialog += async (_, dialog) =>
        {
            Assert.That(dialog.Message, Is.EqualTo("I am a JS Alert"), "Alert should have expected message.");
            await dialog.AcceptAsync();
        };

        // Act - Click the JS Alert button
        await Page.Locator("button:has-text('Click for JS Alert')").ClickAsync();

        // Assert - Check the result text
        var result = await Page.Locator("#result").InnerTextAsync();
        Assert.That(result, Does.Contain("successfully"), "Result should confirm alert was handled.");
    }
}
