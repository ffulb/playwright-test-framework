using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightFramework.Config;
using PlaywrightFramework.Pages;
using Reqnroll;

namespace PlaywrightFramework.StepDefinitions;

/// <summary>
/// Step definitions for navigation-related Gherkin steps.
/// Reusable across any feature that involves page navigation.
/// </summary>
[Binding]
public sealed class NavigationSteps
{
    private readonly IPage _page;
    private readonly TestSettings _settings;

    public NavigationSteps(ScenarioContext scenarioContext)
    {
        _page = scenarioContext.Get<IPage>();
        _settings = scenarioContext.Get<TestSettings>();
    }

    [When("I navigate to {string}")]
    public async Task WhenINavigateTo(string path)
    {
        var url = _settings.BaseUrl.TrimEnd('/') + path;
        await _page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    [Then("the page title should not be empty")]
    public async Task ThenThePageTitleShouldNotBeEmpty()
    {
        var title = await _page.TitleAsync();
        Assert.That(title, Is.Not.Empty, "Page should have a title.");
    }

    [Then("the URL should contain {string}")]
    public void ThenTheUrlShouldContain(string expected)
    {
        Assert.That(_page.Url, Does.Contain(expected), $"URL should contain '{expected}'.");
    }

    [Then("the page should contain text {string}")]
    public async Task ThenThePageShouldContainText(string text)
    {
        var content = await _page.ContentAsync();
        Assert.That(content, Does.Contain(text), $"Page should contain text '{text}'.");
    }
}
