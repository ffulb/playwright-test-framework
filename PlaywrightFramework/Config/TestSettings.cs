namespace PlaywrightFramework.Config;

/// <summary>
/// Strongly-typed configuration model bound from appsettings.json.
/// Change values in appsettings.json to point the framework at your application.
/// </summary>
public sealed class TestSettings
{
    /// <summary>Base URL of the application under test.</summary>
    public string BaseUrl { get; set; } = "https://the-internet.herokuapp.com";

    /// <summary>Browser configuration.</summary>
    public BrowserSettings Browser { get; set; } = new();

    /// <summary>Default credentials for login tests.</summary>
    public CredentialSettings Credentials { get; set; } = new();

    /// <summary>Microsoft Entra ID (Azure AD) authentication settings.</summary>
    public EntraAuthSettings EntraAuth { get; set; } = new();

    /// <summary>Self-healing locator configuration.</summary>
    public SelfHealingSettings SelfHealing { get; set; } = new();

    /// <summary>Test reporting configuration.</summary>
    public ReportingSettings Reporting { get; set; } = new();
}

/// <summary>Browser launch options.</summary>
public sealed class BrowserSettings
{
    /// <summary>Browser engine: Chromium, Firefox, or Webkit.</summary>
    public string Type { get; set; } = "Chromium";

    /// <summary>Run browser in headless mode.</summary>
    public bool Headless { get; set; } = true;

    /// <summary>Slow down operations by this many milliseconds (useful for demos).</summary>
    public float SlowMo { get; set; }

    /// <summary>Default timeout in milliseconds for Playwright actions.</summary>
    public float Timeout { get; set; } = 30_000;

    /// <summary>Browser viewport width.</summary>
    public int ViewportWidth { get; set; } = 1920;

    /// <summary>Browser viewport height.</summary>
    public int ViewportHeight { get; set; } = 1080;
}

/// <summary>Default login credentials.</summary>
public sealed class CredentialSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Microsoft Entra ID authentication settings.</summary>
public sealed class EntraAuthSettings
{
    /// <summary>Enable Entra authentication flow.</summary>
    public bool Enabled { get; set; }

    /// <summary>Azure AD tenant ID.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Application (client) ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>User principal name for automated login.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Password for automated login.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>TOTP secret for MFA bypass (leave empty to skip).</summary>
    public string MfaSecret { get; set; } = string.Empty;

    /// <summary>Path to save/load browser storage state for session reuse.</summary>
    public string StorageStatePath { get; set; } = ".auth/entra-state.json";
}

/// <summary>Self-healing locator configuration.</summary>
public sealed class SelfHealingSettings
{
    /// <summary>Enable AI-assisted self-healing locators.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum healing attempts per locator failure.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Log healed locators for review.</summary>
    public bool LogHealedLocators { get; set; } = true;

    /// <summary>File path for the healing report.</summary>
    public string HealingReportPath { get; set; } = "TestResults/healing-report.json";
}

/// <summary>Test reporting settings.</summary>
public sealed class ReportingSettings
{
    /// <summary>Directory for test results and artifacts.</summary>
    public string OutputDirectory { get; set; } = "TestResults";

    /// <summary>Capture screenshot on test failure.</summary>
    public bool ScreenshotOnFailure { get; set; } = true;

    /// <summary>Capture Playwright trace on test failure.</summary>
    public bool TraceOnFailure { get; set; } = true;
}
