using System.Text;
using System.Text.RegularExpressions;

namespace PlaywrightFramework.Utilities;

/// <summary>
/// AI-powered test script generator. Creates Page Object + NUnit test code
/// from natural language descriptions of test scenarios.
/// </summary>
public static class TestGenerator
{
    /// <summary>
    /// Generates a Page Object class from a page description.
    /// </summary>
    /// <param name="pageName">Name of the page (e.g., "Dashboard", "Settings").</param>
    /// <param name="baseUrl">The page's relative URL path.</param>
    /// <param name="elements">Dictionary of element name → CSS selector.</param>
    /// <returns>Generated C# Page Object code.</returns>
    public static string GeneratePageObject(
        string pageName,
        string baseUrl,
        Dictionary<string, ElementInfo> elements)
    {
        var className = SanitizeName(pageName) + "Page";
        var sb = new StringBuilder();

        sb.AppendLine("using Microsoft.Playwright;");
        sb.AppendLine("using PlaywrightFramework.Config;");
        sb.AppendLine("using PlaywrightFramework.Core;");
        sb.AppendLine();
        sb.AppendLine("namespace PlaywrightFramework.Pages;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {className} : BasePage");
        sb.AppendLine("{");
        sb.AppendLine($"    private const string PagePath = \"{baseUrl}\";");
        sb.AppendLine();

        // Locator constants
        foreach (var (name, info) in elements)
        {
            var constName = SanitizeName(name) + "Selector";
            sb.AppendLine($"    private const string {constName} = \"{EscapeString(info.Selector)}\";");
        }

        sb.AppendLine();
        sb.AppendLine($"    public {className}(IPage page, TestSettings settings) : base(page, settings) {{ }}");
        sb.AppendLine();

        // GoTo method
        sb.AppendLine($"    public async Task GoToAsync()");
        sb.AppendLine("    {");
        sb.AppendLine("        await NavigateAsync(PagePath);");
        sb.AppendLine("    }");

        // Element interaction methods
        foreach (var (name, info) in elements)
        {
            var methodName = SanitizeName(name);
            var constName = methodName + "Selector";
            sb.AppendLine();

            switch (info.Type)
            {
                case ElementType.Button:
                    sb.AppendLine($"    public async Task Click{methodName}Async()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        await Heal.ClickAsync({constName}, \"{name}\");");
                    sb.AppendLine("    }");
                    break;

                case ElementType.Input:
                    sb.AppendLine($"    public async Task Fill{methodName}Async(string value)");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        await Heal.FillAsync({constName}, value, \"{name}\");");
                    sb.AppendLine("    }");
                    break;

                case ElementType.Text:
                    sb.AppendLine($"    public async Task<string> Get{methodName}TextAsync()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        return await Heal.InnerTextAsync({constName}, \"{name}\");");
                    sb.AppendLine("    }");
                    break;

                case ElementType.Dropdown:
                    sb.AppendLine($"    public async Task Select{methodName}Async(string value)");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        var locator = await Heal.FindAsync({constName}, \"{name}\");");
                    sb.AppendLine("        await locator.SelectOptionAsync(value);");
                    sb.AppendLine("    }");
                    break;

                case ElementType.Checkbox:
                    sb.AppendLine($"    public async Task Toggle{methodName}Async()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        await Heal.ClickAsync({constName}, \"{name}\");");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                    sb.AppendLine($"    public async Task<bool> Is{methodName}CheckedAsync()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        var locator = await Heal.FindAsync({constName}, \"{name}\");");
                    sb.AppendLine("        return await locator.IsCheckedAsync();");
                    sb.AppendLine("    }");
                    break;

                default:
                    sb.AppendLine($"    public async Task<bool> Is{methodName}VisibleAsync()");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        return await Heal.IsVisibleAsync({constName}, \"{name}\");");
                    sb.AppendLine("    }");
                    break;
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates NUnit test class from test scenario descriptions.
    /// </summary>
    /// <param name="testClassName">Name of the test class.</param>
    /// <param name="pageName">Name of the associated page object.</param>
    /// <param name="scenarios">List of test scenarios to generate.</param>
    /// <returns>Generated C# NUnit test code.</returns>
    public static string GenerateTestClass(
        string testClassName,
        string pageName,
        List<TestScenario> scenarios)
    {
        var pageClassName = SanitizeName(pageName) + "Page";
        var sanitizedTestName = SanitizeName(testClassName);
        var sb = new StringBuilder();

        sb.AppendLine("using NUnit.Framework;");
        sb.AppendLine("using PlaywrightFramework.Pages;");
        sb.AppendLine();
        sb.AppendLine("namespace PlaywrightFramework.Tests;");
        sb.AppendLine();
        sb.AppendLine($"[TestFixture]");
        sb.AppendLine($"[Category(\"{sanitizedTestName}\")]");
        sb.AppendLine($"[Parallelizable(ParallelScope.Self)]");
        sb.AppendLine($"public sealed class {sanitizedTestName}Tests : BaseTest");
        sb.AppendLine("{");
        sb.AppendLine($"    private {pageClassName} _page = null!;");
        sb.AppendLine();
        sb.AppendLine("    [SetUp]");
        sb.AppendLine("    public void InitPage()");
        sb.AppendLine("    {");
        sb.AppendLine($"        _page = new {pageClassName}(Page, Settings);");
        sb.AppendLine("    }");

        foreach (var scenario in scenarios)
        {
            var methodName = SanitizeName(scenario.Name);
            sb.AppendLine();
            sb.AppendLine($"    [Test]");
            sb.AppendLine($"    [Description(\"{EscapeString(scenario.Description)}\")]");
            sb.AppendLine($"    public async Task {methodName}()");
            sb.AppendLine("    {");
            sb.AppendLine("        // Arrange");
            sb.AppendLine("        await _page.GoToAsync();");
            sb.AppendLine();
            sb.AppendLine("        // Act & Assert");

            foreach (var step in scenario.Steps)
            {
                sb.AppendLine($"        {step}");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates a complete test from a natural language description by analyzing
    /// the target URL and creating appropriate Page Object + Test scaffolding.
    /// </summary>
    /// <param name="description">Natural language test description.</param>
    /// <param name="targetUrl">The URL path to test.</param>
    /// <returns>Tuple of (pageObjectCode, testCode).</returns>
    public static (string PageObject, string TestClass) GenerateFromDescription(
        string description,
        string targetUrl)
    {
        var pageName = InferPageName(targetUrl);
        var elements = InferElements(description);
        var scenarios = InferScenarios(description, pageName);

        var pageObj = GeneratePageObject(pageName, targetUrl, elements);
        var testClass = GenerateTestClass(pageName, pageName, scenarios);

        return (pageObj, testClass);
    }

    #region Private helpers

    private static string InferPageName(string url)
    {
        var segments = url.Trim('/').Split('/');
        var last = segments.Length > 0 ? segments[^1] : "Generated";
        return string.IsNullOrEmpty(last) ? "Generated" : SanitizeName(last);
    }

    private static Dictionary<string, ElementInfo> InferElements(string description)
    {
        var elements = new Dictionary<string, ElementInfo>();
        var lower = description.ToLowerInvariant();

        // Detect common patterns
        if (lower.Contains("login") || lower.Contains("sign in"))
        {
            elements["Username"] = new ElementInfo("#username", ElementType.Input);
            elements["Password"] = new ElementInfo("#password", ElementType.Input);
            elements["LoginButton"] = new ElementInfo("button[type='submit']", ElementType.Button);
        }

        if (lower.Contains("form") || lower.Contains("submit"))
        {
            if (!elements.ContainsKey("SubmitButton"))
                elements["SubmitButton"] = new ElementInfo("button[type='submit']", ElementType.Button);
        }

        if (lower.Contains("search"))
        {
            elements["SearchInput"] = new ElementInfo("input[type='search'], input[name='search'], #search", ElementType.Input);
            elements["SearchButton"] = new ElementInfo("button[type='submit'], .search-btn", ElementType.Button);
        }

        if (lower.Contains("dropdown") || lower.Contains("select"))
        {
            elements["Dropdown"] = new ElementInfo("select", ElementType.Dropdown);
        }

        if (lower.Contains("checkbox"))
        {
            elements["Checkbox"] = new ElementInfo("input[type='checkbox']", ElementType.Checkbox);
        }

        if (lower.Contains("heading") || lower.Contains("title"))
        {
            elements["Heading"] = new ElementInfo("h1, h2", ElementType.Text);
        }

        if (lower.Contains("message") || lower.Contains("alert") || lower.Contains("notification"))
        {
            elements["Message"] = new ElementInfo(".alert, .message, .notification, #flash", ElementType.Text);
        }

        // Default: at least check page loaded
        if (elements.Count == 0)
        {
            elements["PageContent"] = new ElementInfo("body", ElementType.Text);
        }

        return elements;
    }

    private static List<TestScenario> InferScenarios(string description, string pageName)
    {
        var scenarios = new List<TestScenario>();
        var lower = description.ToLowerInvariant();

        // Always generate a page-load test
        scenarios.Add(new TestScenario
        {
            Name = $"{pageName}_ShouldLoad",
            Description = $"Verify {pageName} page loads successfully",
            Steps = new List<string>
            {
                "var title = await _page.GetTitleAsync();",
                "Assert.That(title, Is.Not.Empty, \"Page should have a title.\");"
            }
        });

        if (lower.Contains("login"))
        {
            scenarios.Add(new TestScenario
            {
                Name = $"{pageName}_ValidLogin_ShouldSucceed",
                Description = "Login with valid credentials succeeds",
                Steps = new List<string>
                {
                    "await _page.FillUsernameAsync(Settings.Credentials.Username);",
                    "await _page.FillPasswordAsync(Settings.Credentials.Password);",
                    "await _page.ClickLoginButtonAsync();",
                    "// Assert success - update selector for your app",
                    "var url = _page.GetCurrentUrl();",
                    "Assert.That(url, Does.Not.Contain(\"/login\"), \"Should navigate away from login on success.\");"
                }
            });
        }

        if (lower.Contains("search"))
        {
            scenarios.Add(new TestScenario
            {
                Name = $"{pageName}_Search_ShouldReturnResults",
                Description = "Search returns relevant results",
                Steps = new List<string>
                {
                    "await _page.FillSearchInputAsync(\"test query\");",
                    "await _page.ClickSearchButtonAsync();",
                    "await _page.WaitForLoadAsync();",
                    "// Assert results displayed - update for your app"
                }
            });
        }

        if (lower.Contains("form") || lower.Contains("submit"))
        {
            scenarios.Add(new TestScenario
            {
                Name = $"{pageName}_FormSubmit_ShouldSucceed",
                Description = "Form submission completes successfully",
                Steps = new List<string>
                {
                    "// Fill form fields - update for your app",
                    "await _page.ClickSubmitButtonAsync();",
                    "await _page.WaitForLoadAsync();",
                    "// Assert success message or redirect"
                }
            });
        }

        return scenarios;
    }

    private static string SanitizeName(string name)
    {
        // PascalCase from various separators
        var words = Regex.Split(name, @"[\s_\-/]+")
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => char.ToUpper(w[0]) + w[1..].ToLower());
        var result = string.Join("", words);
        // Remove non-alphanumeric
        return Regex.Replace(result, @"[^a-zA-Z0-9]", "");
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    #endregion
}

/// <summary>
/// Information about a page element for code generation.
/// </summary>
public sealed class ElementInfo
{
    public string Selector { get; }
    public ElementType Type { get; }

    public ElementInfo(string selector, ElementType type)
    {
        Selector = selector;
        Type = type;
    }
}

/// <summary>
/// Type of UI element for generating appropriate interaction methods.
/// </summary>
public enum ElementType
{
    Button,
    Input,
    Text,
    Dropdown,
    Checkbox,
    Link,
    Generic
}

/// <summary>
/// A test scenario with steps for code generation.
/// </summary>
public sealed class TestScenario
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Steps { get; set; } = new();
}
