using Microsoft.Playwright;
using PlaywrightFramework.Config;
using PlaywrightFramework.Core;

namespace PlaywrightFramework.Pages;

/// <summary>
/// Page Object for the login page. Demonstrates self-healing locators
/// with multiple fallback strategies for each element.
///
/// Demo site: https://the-internet.herokuapp.com/login
/// Adapt selectors to your application's login page.
/// </summary>
public sealed class LoginPage : BasePage
{
    // Primary locators (these are what you maintain)
    private const string UsernameInput = "#username";
    private const string PasswordInput = "#password";
    private const string LoginButton = "button[type='submit']";
    private const string FlashMessage = "#flash";
    private const string LogoutButton = "a[href='/logout']";

    // Self-healing alternatives (fallbacks if primary locators break)
    private static readonly AlternativeLocators UsernameAlternatives = new()
    {
        DataTestId = "username",
        AriaLabel = "Username",
        CssSelector = "input[name='username']",
        XPath = "//input[@id='username']"
    };

    private static readonly AlternativeLocators PasswordAlternatives = new()
    {
        DataTestId = "password",
        AriaLabel = "Password",
        CssSelector = "input[name='password']",
        XPath = "//input[@type='password']"
    };

    private static readonly AlternativeLocators LoginButtonAlternatives = new()
    {
        Text = "Login",
        AriaLabel = "Login",
        CssSelector = "button.radius",
        XPath = "//button[@type='submit']"
    };

    /// <summary>
    /// Initializes the LoginPage page object.
    /// </summary>
    public LoginPage(IPage page, TestSettings settings) : base(page, settings) { }

    /// <summary>
    /// Navigates to the login page.
    /// </summary>
    public async Task GoToAsync()
    {
        await NavigateAsync("/login");
    }

    /// <summary>
    /// Enters the username into the username field.
    /// Uses self-healing to find the input even if selectors change.
    /// </summary>
    /// <param name="username">The username to enter.</param>
    public async Task EnterUsernameAsync(string username)
    {
        await Heal.FillAsync(UsernameInput, username, "Username Input", UsernameAlternatives);
    }

    /// <summary>
    /// Enters the password into the password field.
    /// </summary>
    /// <param name="password">The password to enter.</param>
    public async Task EnterPasswordAsync(string password)
    {
        await Heal.FillAsync(PasswordInput, password, "Password Input", PasswordAlternatives);
    }

    /// <summary>
    /// Clicks the login button.
    /// </summary>
    public async Task ClickLoginAsync()
    {
        await Heal.ClickAsync(LoginButton, "Login Button", LoginButtonAlternatives);
    }

    /// <summary>
    /// Performs a complete login with the given credentials.
    /// </summary>
    /// <param name="username">Username to log in with.</param>
    /// <param name="password">Password to log in with.</param>
    public async Task LoginAsync(string username, string password)
    {
        await GoToAsync();
        await EnterUsernameAsync(username);
        await EnterPasswordAsync(password);
        await ClickLoginAsync();
        await WaitForLoadAsync();
    }

    /// <summary>
    /// Performs login using credentials from appsettings.json.
    /// </summary>
    public async Task LoginWithConfigCredentialsAsync()
    {
        await LoginAsync(Settings.Credentials.Username, Settings.Credentials.Password);
    }

    /// <summary>
    /// Gets the flash message text displayed after login attempt.
    /// </summary>
    /// <returns>The flash message content.</returns>
    public async Task<string> GetFlashMessageAsync()
    {
        return await Heal.InnerTextAsync(FlashMessage, "Flash Message", new AlternativeLocators
        {
            CssSelector = ".flash",
            DataTestId = "flash"
        });
    }

    /// <summary>
    /// Checks if the login was successful by looking for the success indicator.
    /// </summary>
    /// <returns>True if login succeeded.</returns>
    public async Task<bool> IsLoginSuccessfulAsync()
    {
        var message = await GetFlashMessageAsync();
        return message.Contains("You logged into a secure area", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a login error is displayed.
    /// </summary>
    /// <returns>True if an error message is shown.</returns>
    public async Task<bool> IsLoginErrorDisplayedAsync()
    {
        var message = await GetFlashMessageAsync();
        return message.Contains("invalid", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the logout button is visible (indicates authenticated state).
    /// </summary>
    public async Task<bool> IsLogoutButtonVisibleAsync()
    {
        return await Heal.IsVisibleAsync(LogoutButton, "Logout Button", new AlternativeLocators
        {
            Text = "Logout",
            CssSelector = "a.button"
        });
    }

    /// <summary>
    /// Clicks the logout button.
    /// </summary>
    public async Task LogoutAsync()
    {
        await Heal.ClickAsync(LogoutButton, "Logout Button", new AlternativeLocators
        {
            Text = "Logout",
            CssSelector = "a.button"
        });
        await WaitForLoadAsync();
    }
}
