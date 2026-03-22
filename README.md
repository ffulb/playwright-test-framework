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
- **AI Test Generator** - Generate Page Object + NUnit test code from natural language descriptions
- **Leadership Notifications** - Auto-email HTML test reports to your leadership team after each run

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

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Test Execution                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │
│  │ LoginTests   │  │ Navigation   │  │ [Your Tests]             │  │
│  │              │  │ Tests        │  │                          │  │
│  └──────┬───────┘  └──────┬───────┘  └────────────┬─────────────┘  │
│         └─────────────────┼───────────────────────┘                 │
│                           │ extends                                 │
│                    ┌──────┴───────┐                                 │
│                    │   BaseTest   │ ← NUnit lifecycle, browser      │
│                    │              │   setup/teardown, artifacts      │
│                    └──────┬───────┘                                 │
└───────────────────────────┼─────────────────────────────────────────┘
                            │ uses
┌───────────────────────────┼─────────────────────────────────────────┐
│                     Page Objects                                     │
│                    ┌──────┴───────┐                                  │
│                    │   BasePage   │ ← Navigation, screenshots,      │
│                    │              │   self-healing access            │
│                    └──────┬───────┘                                  │
│         ┌─────────────────┼───────────────────────┐                 │
│  ┌──────┴───────┐  ┌──────┴───────┐  ┌────────────┴─────────────┐  │
│  │  LoginPage   │  │ [YourPages]  │  │ Generated Pages (CLI)    │  │
│  └──────────────┘  └──────────────┘  └──────────────────────────┘  │
└─────────────────────────────┬───────────────────────────────────────┘
                              │ uses
┌─────────────────────────────┼───────────────────────────────────────┐
│                        Core Engine                                   │
│  ┌──────────────────┐  ┌───┴──────────┐  ┌──────────────────────┐  │
│  │  BrowserFactory  │  │ SelfHealing  │  │    EntraAuth         │  │
│  │                  │  │ Locator      │  │                      │  │
│  │ • Chromium       │  │              │  │ • Email/Password     │  │
│  │ • Firefox        │  │ • Primary    │  │ • TOTP MFA           │  │
│  │ • WebKit         │  │ • data-testid│  │ • Consent handling   │  │
│  │ • Context mgmt   │  │ • aria-label │  │ • Session persist    │  │
│  │ • Session reuse  │  │ • text       │  │                      │  │
│  │                  │  │ • CSS/XPath  │  │                      │  │
│  │                  │  │ • Auto-derive│  │                      │  │
│  └──────────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────┬───────────────────────────────────────┘
                              │ uses
┌─────────────────────────────┼───────────────────────────────────────┐
│                       Utilities                                      │
│  ┌──────────────────┐  ┌───┴──────────┐  ┌──────────────────────┐  │
│  │ TestGenerator    │  │ ReportGen    │  │ NotificationService  │  │
│  │                  │  │              │  │                      │  │
│  │ • NL → Page+Test │  │ • HTML report│  │ • SMTP email         │  │
│  │ • Element-based  │  │ • Healing    │  │ • Pass/fail metrics  │  │
│  │ • Interactive CLI│  │   summary    │  │ • Leadership alerts  │  │
│  └──────────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────┬───────────────────────────────────────┘
                              │ reads
┌─────────────────────────────┼───────────────────────────────────────┐
│                      Configuration                                   │
│  ┌──────────────────────────┴──────────────────────────────────────┐│
│  │                    appsettings.json                              ││
│  │  BaseUrl · Browser · Credentials · EntraAuth · SelfHealing      ││
│  │  Reporting · Notification                                       ││
│  └─────────────────────────────────────────────────────────────────┘│
│  ┌──────────────────────────┐  ┌───────────────────────────────────┐│
│  │   TestSettings.cs        │  │  Environment Variable Overrides   ││
│  │   (strongly-typed model) │  │  TEST_BASE_URL, TEST_HEADLESS,    ││
│  │                          │  │  TEST_ENVIRONMENT                 ││
│  └──────────────────────────┘  └───────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        CI/CD Pipelines                               │
│  ┌──────────────────┐           ┌──────────────────────────────┐   │
│  │  GitHub Actions   │           │  Azure DevOps Pipelines      │   │
│  │  (.github/        │           │  (azure-pipelines.yml)       │   │
│  │   workflows/)     │           │                              │   │
│  └──────────────────┘           └──────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Test starts** → `BaseTest` loads `appsettings.json` → launches browser via `BrowserFactory`
2. **Entra Auth** (if enabled) → authenticates once → saves session for reuse
3. **Test runs** → Page Objects use `SelfHealingLocator` → if selector breaks, auto-heals and logs
4. **Test ends** → captures screenshots/traces on failure → generates HTML report
5. **Notification** (if enabled) → emails pass/fail summary to leadership team

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

## AI Test Generator

### CLI Tool (Interactive)

The fastest way to generate tests — just describe what you want:

```bash
# Quick generate from a prompt
cd PlaywrightFramework.Cli
dotnet run -- generate "Test the login page with email and password" /login

# Interactive mode — keep generating tests in a loop
dotnet run -- interactive

# Guided element-based generation
dotnet run -- elements
```

**Example:**
```
> dotnet run -- generate "Test checkout with shipping and payment validation" /checkout

Generating tests for: Test checkout with shipping and payment validation
Target URL path: /checkout

── Page Object: Generated/CheckoutPage.cs ──
── Test Class: Generated/CheckoutTests.cs ──

Files saved to: Generated/
Copy these files to Pages/ and Tests/ folders, then customize the selectors for your app.
```

### Programmatic Usage

```csharp
using PlaywrightFramework.Utilities;

// Generate Page Object + Test from description
var (pageCode, testCode) = TestGenerator.GenerateFromDescription(
    "Test the login page with valid and invalid credentials",
    "/login"
);

// Or build with explicit elements
var elements = new Dictionary<string, ElementInfo>
{
    ["SearchInput"] = new ElementInfo("#search", ElementType.Input),
    ["SearchButton"] = new ElementInfo(".search-btn", ElementType.Button),
    ["Results"] = new ElementInfo("#results", ElementType.Text)
};

var pageObject = TestGenerator.GeneratePageObject("Search", "/search", elements);

var scenarios = new List<TestScenario>
{
    new TestScenario
    {
        Name = "Search_ShouldReturnResults",
        Description = "Searching returns matching results",
        Steps = new List<string>
        {
            "await _page.FillSearchInputAsync(\"test\");",
            "await _page.ClickSearchButtonAsync();",
            "var text = await _page.GetResultsTextAsync();",
            "Assert.That(text, Does.Contain(\"test\"));"
        }
    }
};

var testClass = TestGenerator.GenerateTestClass("Search", "Search", scenarios);
```

## Leadership Notifications

Auto-send test results to your leadership team via email after each run.

### Setup

In `appsettings.json`:

```json
"Notification": {
  "Enabled": true,
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587,
  "SmtpUsername": "your-email@gmail.com",
  "SmtpPassword": "your-app-password",
  "UseSsl": true,
  "FromAddress": "your-email@gmail.com",
  "FromName": "Playwright Test Automation",
  "Recipients": [
    "lead1@company.com",
    "lead2@company.com"
  ]
}
```

When enabled, a styled HTML email with pass/fail metrics and attached report is sent automatically after each test run.

## Adding New Tests

1. Create a page object in `Pages/` extending `BasePage`
2. Define selectors with `AlternativeLocators` for self-healing
3. Create a test class in `Tests/` extending `BaseTest`
4. Use `Page`, `Settings`, and page objects from the base class
5. Or use `TestGenerator` to scaffold from a description

## License

MIT
