using Microsoft.Playwright;
using PlaywrightFramework.Config;

namespace PlaywrightFramework.Core;

/// <summary>
/// Automates Microsoft Entra ID (Azure AD) login flow for applications
/// that use Entra as their identity provider. Handles the redirect-based
/// OAuth login including email entry, password entry, and optional MFA/consent.
///
/// Usage:
///   1. Set EntraAuth.Enabled = true in appsettings.json
///   2. Provide TenantId, Username, and Password
///   3. The framework will authenticate once and save browser state for reuse
/// </summary>
public sealed class EntraAuth
{
    private readonly EntraAuthSettings _settings;

    // Microsoft login page selectors (stable across Entra tenants)
    private const string EmailInput = "input[type='email'][name='loginfmt']";
    private const string PasswordInput = "input[type='password'][name='passwd']";
    private const string NextButton = "input[type='submit'][value='Next']";
    private const string SignInButton = "input[type='submit'][value='Sign in']";
    private const string StaySignedInNo = "input[type='button'][value='No']";
    private const string StaySignedInYes = "input[type='submit'][value='Yes']";
    private const string MfaCodeInput = "input[name='otc']";
    private const string MfaVerifyButton = "input[type='submit'][value='Verify']";

    /// <summary>
    /// Initializes a new Entra authentication handler.
    /// </summary>
    /// <param name="settings">Entra authentication configuration.</param>
    public EntraAuth(EntraAuthSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Performs the full Microsoft Entra login flow on the given page.
    /// Navigates through email -> password -> optional MFA -> consent screens.
    /// </summary>
    /// <param name="page">The Playwright page (should be on the app's login page or redirected to login.microsoftonline.com).</param>
    /// <param name="appLoginUrl">URL that triggers the Entra redirect (e.g., your app's /login endpoint).</param>
    /// <returns>Task that completes when authentication is finished.</returns>
    public async Task AuthenticateAsync(IPage page, string appLoginUrl)
    {
        // Check if we have a saved session that's still valid
        if (await TryRestoreSessionAsync(page, appLoginUrl))
        {
            Console.WriteLine("[EntraAuth] Session restored from saved state.");
            return;
        }

        Console.WriteLine("[EntraAuth] Starting fresh authentication flow...");

        // Navigate to the app's login page (triggers Entra redirect)
        await page.GotoAsync(appLoginUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for Microsoft login page to load
        await page.WaitForURLAsync(url => url.Contains("login.microsoftonline.com") || url.Contains("login.microsoft.com"),
            new() { Timeout = 15000 });

        // Step 1: Enter email address
        Console.WriteLine("[EntraAuth] Entering email...");
        await page.Locator(EmailInput).WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await page.Locator(EmailInput).FillAsync(_settings.Username);
        await page.Locator(NextButton).ClickAsync();

        // Step 2: Wait for password page (may have a brief redirect for federated orgs)
        Console.WriteLine("[EntraAuth] Entering password...");
        await page.Locator(PasswordInput).WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await page.Locator(PasswordInput).FillAsync(_settings.Password);
        await page.Locator(SignInButton).ClickAsync();

        // Step 3: Handle MFA if configured
        if (!string.IsNullOrEmpty(_settings.MfaSecret))
        {
            await HandleMfaAsync(page);
        }

        // Step 4: Handle "Stay signed in?" prompt
        await HandleStaySignedInAsync(page);

        // Step 5: Handle consent screen if it appears
        await HandleConsentAsync(page);

        // Step 6: Wait for redirect back to the application
        await page.WaitForURLAsync(
            url => !url.Contains("login.microsoftonline.com") && !url.Contains("login.microsoft.com"),
            new() { Timeout = 30000 });

        Console.WriteLine($"[EntraAuth] Authentication complete. Redirected to: {page.Url}");

        // Save browser state for session reuse
        await SaveSessionAsync(page.Context);
    }

    /// <summary>
    /// Checks if a saved authentication state exists and is usable.
    /// </summary>
    /// <param name="page">The Playwright page.</param>
    /// <param name="appUrl">The application URL to verify the session against.</param>
    /// <returns>True if the saved session is valid.</returns>
    private async Task<bool> TryRestoreSessionAsync(IPage page, string appUrl)
    {
        if (!File.Exists(_settings.StorageStatePath))
            return false;

        try
        {
            // Navigate to the app and check if we're already authenticated
            await page.GotoAsync(appUrl, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 10000 });

            // If we weren't redirected to the login page, session is valid
            var url = page.Url;
            return !url.Contains("login.microsoftonline.com") && !url.Contains("login.microsoft.com");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handles the TOTP MFA challenge during login.
    /// </summary>
    private async Task HandleMfaAsync(IPage page)
    {
        try
        {
            await page.Locator(MfaCodeInput).WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            var totpCode = GenerateTotpCode(_settings.MfaSecret);
            Console.WriteLine("[EntraAuth] Entering MFA code...");

            await page.Locator(MfaCodeInput).FillAsync(totpCode);
            await page.Locator(MfaVerifyButton).ClickAsync();
        }
        catch (TimeoutException)
        {
            Console.WriteLine("[EntraAuth] No MFA prompt detected, continuing...");
        }
    }

    /// <summary>
    /// Handles the "Stay signed in?" prompt by clicking No (for test isolation).
    /// </summary>
    private static async Task HandleStaySignedInAsync(IPage page)
    {
        try
        {
            // Look for either "Yes" or "No" button on the "Stay signed in?" page
            var noButton = page.Locator(StaySignedInNo);
            var yesButton = page.Locator(StaySignedInYes);

            // Wait for either to appear
            await page.WaitForSelectorAsync($"{StaySignedInNo}, {StaySignedInYes}",
                new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            // Click "Yes" to stay signed in (better for session reuse)
            if (await yesButton.IsVisibleAsync())
            {
                await yesButton.ClickAsync();
                Console.WriteLine("[EntraAuth] Clicked 'Yes' on Stay signed in.");
            }
            else if (await noButton.IsVisibleAsync())
            {
                await noButton.ClickAsync();
                Console.WriteLine("[EntraAuth] Clicked 'No' on Stay signed in.");
            }
        }
        catch (TimeoutException)
        {
            Console.WriteLine("[EntraAuth] No 'Stay signed in?' prompt detected.");
        }
    }

    /// <summary>
    /// Handles the Azure AD consent screen if it appears (first-time app access).
    /// </summary>
    private static async Task HandleConsentAsync(IPage page)
    {
        try
        {
            var acceptButton = page.Locator("input[type='submit'][value='Accept']");
            await acceptButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 });
            await acceptButton.ClickAsync();
            Console.WriteLine("[EntraAuth] Accepted consent prompt.");
        }
        catch (TimeoutException)
        {
            // No consent screen, that's fine
        }
    }

    /// <summary>
    /// Saves the browser context storage state for session reuse across tests.
    /// </summary>
    private async Task SaveSessionAsync(IBrowserContext context)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settings.StorageStatePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await context.StorageStateAsync(new() { Path = _settings.StorageStatePath });
            Console.WriteLine($"[EntraAuth] Session state saved to {_settings.StorageStatePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EntraAuth] Warning: Could not save session state: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a TOTP code from the provided secret.
    /// This is a simplified implementation for automated testing.
    /// For production use, consider using a dedicated TOTP library (e.g., Otp.NET).
    /// </summary>
    /// <param name="secret">Base32-encoded TOTP secret.</param>
    /// <returns>6-digit TOTP code.</returns>
    private static string GenerateTotpCode(string secret)
    {
        // Simplified TOTP - in production, use Otp.NET NuGet package
        var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timeStep = epoch / 30;

        var keyBytes = Base32Decode(secret);
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using var hmac = new System.Security.Cryptography.HMACSHA1(keyBytes);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var truncated = ((hash[offset] & 0x7F) << 24)
                      | ((hash[offset + 1] & 0xFF) << 16)
                      | ((hash[offset + 2] & 0xFF) << 8)
                      | (hash[offset + 3] & 0xFF);

        var code = truncated % 1_000_000;
        return code.ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.TrimEnd('=').ToUpperInvariant();

        var bits = new List<byte>();
        foreach (var c in input)
        {
            var val = alphabet.IndexOf(c);
            if (val < 0) continue;
            for (int i = 4; i >= 0; i--)
                bits.Add((byte)((val >> i) & 1));
        }

        var bytes = new byte[bits.Count / 8];
        for (int i = 0; i < bytes.Length; i++)
        {
            for (int j = 0; j < 8; j++)
                bytes[i] = (byte)((bytes[i] << 1) | bits[i * 8 + j]);
        }
        return bytes;
    }
}
