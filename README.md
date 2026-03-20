# Playwright Test Automation Framework

A production-ready C# test automation framework built on **Microsoft Playwright** with AI-assisted self-healing locators, Microsoft Entra authentication support, and CI/CD pipelines for both GitHub Actions and Azure DevOps.

## Features

- **C# + Playwright (.NET 8)** - Type-safe, fast, cross-browser testing
- **Config-based URL switching** - Change `appsettings.json` to point to any environment
- **Self-healing locators** - When selectors break, the framework automatically tries alternative strategies and logs what was healed
- **Microsoft Entra authentication** - Automates Azure AD login flows (email, password, MFA, consent)
- **Page Object Model** - Clean separation of test logic and page interactions
- **CI/CD ready** - GitHub Actions and Azure DevOps pipeline YAML included
- **Test artifacts** - Automatic screenshots and Playwright traces on failure

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PowerShell (for Playwright browser installation)

### Setup

```bash
# Clone the repository
git clone <your-repo-url>
cd playwright_project

# Restore NuGet packages
dotnet restore

# Build the project
dotnet build

# Install Playwright browsers (Chromium, Firefox, WebKit)
pwsh PlaywrightFramework/bin/Debug/net8.0/playwright.ps1 install --with-deps
```

### Run Tests

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "Category=Login"
dotnet test --filter "Category=Navigation"
dotnet test --filter "Category=SelfHealing"

# Run with custom base URL
TEST_BASE_URL=https://your-app.com dotnet test

# Run with visible browser (non-headless)
TEST_HEADLESS=false dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

## Configuration

All settings are in `PlaywrightFramework/Config/appsettings.json`:

```json
{
  "TestSettings": {
    "BaseUrl": "https://your-application-url.com",
    "Browser": {
      "Type": "Chromium",
      "Headless": true,
      "Timeout": 30000
    },
    "Credentials": {
      "Username": "your-test-user",
      "Password": "your-test-password"
    }
  }
}
```

### Point to Your Application

1. Change `BaseUrl` to your application's URL
2. Update `Credentials` with your test account
3. If using Entra auth, set `EntraAuth.Enabled` to `true` and fill in the tenant/client details
4. Update page object selectors in `Pages/` to match your application's HTML

### Environment Variable Overrides

For CI/CD, you can override settings via environment variables:

| Variable | Description |
|----------|-------------|
| `TEST_BASE_URL` | Override the base URL |
| `TEST_HEADLESS` | `true` or `false` |
| `TEST_ENVIRONMENT` | Loads `appsettings.{env}.json` |

## Self-Healing Locators

When a primary CSS/XPath selector fails, the framework automatically:

1. Tries explicit alternatives defined in the page object (`data-testid`, `aria-label`, `text`, `role`, CSS, XPath)
2. Derives alternatives from the primary selector (e.g., `#username` -> `[name='username']`, `[data-testid='username']`)
3. Falls back to text content matching
4. Logs every healed locator to `TestResults/healing-report.json`

### Define alternatives in page objects:

```csharp
await Heal.ClickAsync(
    "#submit-btn",                          // Primary selector
    "Submit Button",                        // Friendly name for logs
    new AlternativeLocators                 // Fallbacks
    {
        DataTestId = "submit",
        AriaLabel = "Submit form",
        Text = "Submit",
        CssSelector = "button.btn-primary"
    });
```

## Microsoft Entra Authentication

For apps that use Azure AD / Entra ID login:

1. Enable in `appsettings.json`:
   ```json
   "EntraAuth": {
     "Enabled": true,
     "TenantId": "your-tenant-id",
     "Username": "user@domain.com",
     "Password": "password"
   }
   ```

2. The framework handles: email entry, password entry, MFA (if TOTP secret provided), consent screens, and session persistence.

## Project Structure

```
PlaywrightFramework/
├── Config/            - Configuration (appsettings.json, strongly-typed models)
├── Core/              - Framework core (browser factory, self-healing, Entra auth)
├── Pages/             - Page Object Model classes
├── Tests/             - NUnit test classes
└── Utilities/         - Reporting and helper utilities
```

## CI/CD

### GitHub Actions

Tests run automatically on push/PR to `main` and `develop`. Manual runs support custom URL and browser selection.

### Azure DevOps

Import `azure-pipelines.yml` into your Azure DevOps project. Supports parameterized runs with URL and browser selection.

## Adding New Tests

1. Create a page object in `Pages/` extending `BasePage`
2. Define selectors with `AlternativeLocators` for self-healing
3. Create a test class in `Tests/` extending `BaseTest`
4. Use `Page`, `Settings`, and page objects from the base class

## License

MIT
