using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightFramework.Config;
using PlaywrightFramework.Core;
using PlaywrightFramework.Pages;
using Reqnroll;

namespace PlaywrightFramework.StepDefinitions;

/// <summary>
/// Step definitions for login-related Gherkin steps.
/// Maps Given/When/Then steps to LoginPage actions.
/// </summary>
[Binding]
public sealed class LoginSteps
{
    private readonly ScenarioContext _scenarioContext;
    private readonly IPage _page;
    private readonly TestSettings _settings;
    private readonly LoginPage _loginPage;

    public LoginSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
        _page = scenarioContext.Get<IPage>();
        _settings = scenarioContext.Get<TestSettings>();
        _loginPage = new LoginPage(_page, _settings);
    }

    [Given("I am on the login page")]
    public async Task GivenIAmOnTheLoginPage()
    {
        // Skip if Entra is enabled (no traditional login page)
        if (_settings.EntraAuth.Enabled)
        {
            Assert.Ignore("Skipped: Entra authentication handles login via redirect.");
            return;
        }
        await _loginPage.GoToAsync();
    }

    [When("I enter valid credentials")]
    public async Task WhenIEnterValidCredentials()
    {
        await _loginPage.EnterUsernameAsync(_settings.Credentials.Username);
        await _loginPage.EnterPasswordAsync(_settings.Credentials.Password);
    }

    [When("I enter username {string} and password {string}")]
    public async Task WhenIEnterUsernameAndPassword(string username, string password)
    {
        await _loginPage.EnterUsernameAsync(username);
        await _loginPage.EnterPasswordAsync(password);
    }

    [When("I click the login button")]
    public async Task WhenIClickTheLoginButton()
    {
        await _loginPage.ClickLoginAsync();
    }

    [When("I click the logout button")]
    public async Task WhenIClickTheLogoutButton()
    {
        await _loginPage.LogoutAsync();
    }

    [Then("I should be logged in successfully")]
    public async Task ThenIShouldBeLoggedInSuccessfully()
    {
        var isSuccess = await _loginPage.IsLoginSuccessfulAsync();
        Assert.That(isSuccess, Is.True, "Login should succeed.");
    }

    [Then("I should see a login error message")]
    public async Task ThenIShouldSeeALoginErrorMessage()
    {
        var isError = await _loginPage.IsLoginErrorDisplayedAsync();
        Assert.That(isError, Is.True, "Login error message should be displayed.");
    }

    [Then("I should be on the login page")]
    public void ThenIShouldBeOnTheLoginPage()
    {
        var url = _page.Url;
        Assert.That(url, Does.Contain("/login"), "Should be on the login page.");
    }
}
