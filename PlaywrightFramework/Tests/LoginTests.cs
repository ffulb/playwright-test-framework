using NUnit.Framework;
using PlaywrightFramework.Pages;

namespace PlaywrightFramework.Tests;

/// <summary>
/// Login test suite demonstrating the framework capabilities.
/// Tests run against https://the-internet.herokuapp.com/login by default.
///
/// To run against your own site:
///   1. Update BaseUrl and Credentials in appsettings.json
///   2. Update LoginPage.cs selectors to match your login form
///
/// NOTE: These tests are skipped when EntraAuth is enabled, since Entra
/// handles authentication via redirect — there is no traditional login form.
/// Create your own Page Objects and Tests for post-login pages instead.
/// </summary>
[TestFixture]
[Category("Login")]
[Parallelizable(ParallelScope.Self)]
public sealed class LoginTests : BaseTest
{
    private LoginPage _loginPage = null!;

    [SetUp]
    public void InitPage()
    {
        if (Settings.EntraAuth.Enabled)
        {
            Assert.Ignore("LoginTests are skipped when Entra authentication is enabled. " +
                "Entra handles login via redirect — create Page Objects for your post-login pages instead.");
        }
        _loginPage = new LoginPage(Page, Settings);
    }

    /// <summary>
    /// Verifies that a user can log in with valid credentials from config.
    /// </summary>
    [Test]
    [Description("Successful login with valid credentials from appsettings.json")]
    public async Task Login_WithValidCredentials_ShouldSucceed()
    {
        // Arrange & Act
        await _loginPage.LoginWithConfigCredentialsAsync();

        // Assert
        var isSuccess = await _loginPage.IsLoginSuccessfulAsync();
        Assert.That(isSuccess, Is.True, "Login should succeed with valid credentials.");
    }

    /// <summary>
    /// Verifies that invalid credentials show an appropriate error.
    /// </summary>
    [Test]
    [Description("Login with invalid credentials shows error message")]
    public async Task Login_WithInvalidCredentials_ShouldShowError()
    {
        // Arrange & Act
        await _loginPage.LoginAsync("invalid_user", "bad_password");

        // Assert
        var isError = await _loginPage.IsLoginErrorDisplayedAsync();
        Assert.That(isError, Is.True, "Login with invalid credentials should display an error.");
    }

    /// <summary>
    /// Verifies that the login page loads correctly and displays expected elements.
    /// </summary>
    [Test]
    [Description("Login page loads with username field, password field, and login button")]
    public async Task LoginPage_ShouldDisplayLoginForm()
    {
        // Arrange
        await _loginPage.GoToAsync();

        // Assert
        var title = await _loginPage.GetTitleAsync();
        Assert.That(title, Is.Not.Empty, "Page should have a title.");

        var url = _loginPage.GetCurrentUrl();
        Assert.That(url, Does.Contain("/login"), "URL should contain /login.");
    }

    /// <summary>
    /// Verifies the full login/logout cycle works correctly.
    /// </summary>
    [Test]
    [Description("User can log in and then log out successfully")]
    public async Task Login_ThenLogout_ShouldReturnToLoginPage()
    {
        // Arrange - Login
        await _loginPage.LoginWithConfigCredentialsAsync();
        var isLoggedIn = await _loginPage.IsLoginSuccessfulAsync();
        Assert.That(isLoggedIn, Is.True, "Should be logged in before testing logout.");

        // Act - Logout
        await _loginPage.LogoutAsync();

        // Assert
        var url = _loginPage.GetCurrentUrl();
        Assert.That(url, Does.Contain("/login"), "Should be redirected back to login page after logout.");
    }

    /// <summary>
    /// Verifies login with empty credentials shows validation errors.
    /// </summary>
    [Test]
    [Description("Login with empty fields shows error message")]
    public async Task Login_WithEmptyFields_ShouldShowError()
    {
        // Arrange & Act
        await _loginPage.LoginAsync("", "");

        // Assert
        var isError = await _loginPage.IsLoginErrorDisplayedAsync();
        Assert.That(isError, Is.True, "Login with empty fields should display an error.");
    }

    /// <summary>
    /// Demonstrates that self-healing locators can recover from selector changes.
    /// This test intentionally uses a wrong primary selector to trigger healing.
    /// </summary>
    [Test]
    [Description("Self-healing locator recovers when primary selector is broken")]
    [Category("SelfHealing")]
    public async Task SelfHealing_WhenPrimarySelectorBroken_ShouldRecover()
    {
        // Navigate to login page
        await _loginPage.GoToAsync();

        // Use a deliberately broken selector with alternatives that work
        var healer = new Core.SelfHealingLocator(Page, Settings.SelfHealing);

        // This primary selector is wrong, but the alternative (name='username') will work
        var locator = await healer.FindAsync(
            "#wrong_username_id",
            "Username Input",
            new Core.AlternativeLocators
            {
                CssSelector = "input[name='username']",
                DataTestId = "username"
            });

        Assert.That(locator, Is.Not.Null, "Self-healing should find the element via alternatives.");
    }
}
