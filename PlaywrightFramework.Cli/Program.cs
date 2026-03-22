using PlaywrightFramework.Utilities;

namespace PlaywrightFramework.Cli;

/// <summary>
/// Interactive CLI tool for AI-powered test script generation.
///
/// Usage:
///   dotnet run -- generate "Test the login page with email and password" /login
///   dotnet run -- interactive
///   dotnet run -- help
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║   Playwright AI Test Generator              ║");
        Console.WriteLine("║   Generate test scripts from descriptions   ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        if (args.Length == 0 || args[0] == "help" || args[0] == "--help")
        {
            ShowHelp();
            return 0;
        }

        return args[0].ToLower() switch
        {
            "generate" => await HandleGenerate(args),
            "interactive" or "i" => await HandleInteractive(),
            "elements" => await HandleElementBased(args),
            _ => HandleUnknown(args[0])
        };
    }

    static async Task<int> HandleGenerate(string[] args)
    {
        if (args.Length < 3)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Usage: dotnet run -- generate \"<description>\" <url-path>");
            Console.WriteLine("Example: dotnet run -- generate \"Test login with valid and invalid credentials\" /login");
            Console.ResetColor();
            return 1;
        }

        var description = args[1];
        var urlPath = args[2];
        var outputDir = args.Length > 3 ? args[3] : "Generated";

        Console.WriteLine($"Generating tests for: {description}");
        Console.WriteLine($"Target URL path: {urlPath}");
        Console.WriteLine();

        var (pageCode, testCode) = TestGenerator.GenerateFromDescription(description, urlPath);

        Directory.CreateDirectory(outputDir);

        var pageName = InferPageName(urlPath);
        var pageFile = Path.Combine(outputDir, $"{pageName}Page.cs");
        var testFile = Path.Combine(outputDir, $"{pageName}Tests.cs");

        await File.WriteAllTextAsync(pageFile, pageCode);
        await File.WriteAllTextAsync(testFile, testCode);

        PrintSuccess("Page Object", pageFile, pageCode);
        PrintSuccess("Test Class", testFile, testCode);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nFiles saved to: {Path.GetFullPath(outputDir)}/");
        Console.WriteLine("Copy these files to Pages/ and Tests/ folders, then customize the selectors for your app.");
        Console.ResetColor();

        return 0;
    }

    static async Task<int> HandleInteractive()
    {
        Console.WriteLine("Interactive mode — describe your test scenarios and get code generated.");
        Console.WriteLine("Type 'quit' or 'exit' to stop.\n");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("What do you want to test? > ");
            Console.ResetColor();

            var description = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(description) ||
                description.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                description.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("URL path (e.g., /login): > ");
            Console.ResetColor();

            var urlPath = Console.ReadLine()?.Trim() ?? "/";

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Save to directory [Generated]: > ");
            Console.ResetColor();

            var outputDir = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(outputDir)) outputDir = "Generated";

            Console.WriteLine("\nGenerating...\n");

            var (pageCode, testCode) = TestGenerator.GenerateFromDescription(description, urlPath);

            Directory.CreateDirectory(outputDir);

            var pageName = InferPageName(urlPath);
            var pageFile = Path.Combine(outputDir, $"{pageName}Page.cs");
            var testFile = Path.Combine(outputDir, $"{pageName}Tests.cs");

            await File.WriteAllTextAsync(pageFile, pageCode);
            await File.WriteAllTextAsync(testFile, testCode);

            PrintSuccess("Page Object", pageFile, pageCode);
            PrintSuccess("Test Class", testFile, testCode);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nFiles saved to: {Path.GetFullPath(outputDir)}/");
            Console.ResetColor();
            Console.WriteLine(new string('─', 60));
            Console.WriteLine();
        }

        return 0;
    }

    static async Task<int> HandleElementBased(string[] args)
    {
        Console.WriteLine("Element-based generation — define your page elements and get structured code.\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Page name (e.g., Dashboard): > ");
        Console.ResetColor();
        var pageName = Console.ReadLine()?.Trim() ?? "MyPage";

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("URL path (e.g., /dashboard): > ");
        Console.ResetColor();
        var urlPath = Console.ReadLine()?.Trim() ?? "/";

        var elements = new Dictionary<string, ElementInfo>();

        Console.WriteLine("\nAdd elements (type 'done' when finished):");
        Console.WriteLine("  Types: button, input, text, dropdown, checkbox, link");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\nElement name (or 'done'): > ");
            Console.ResetColor();

            var name = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(name) || name.Equals("done", StringComparison.OrdinalIgnoreCase))
                break;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("CSS selector: > ");
            Console.ResetColor();
            var selector = Console.ReadLine()?.Trim() ?? "";

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Type (button/input/text/dropdown/checkbox/link): > ");
            Console.ResetColor();
            var typeStr = Console.ReadLine()?.Trim()?.ToLower() ?? "generic";

            var elementType = typeStr switch
            {
                "button" or "btn" => ElementType.Button,
                "input" or "text-input" or "field" => ElementType.Input,
                "text" or "label" or "heading" => ElementType.Text,
                "dropdown" or "select" => ElementType.Dropdown,
                "checkbox" or "check" => ElementType.Checkbox,
                "link" or "anchor" or "a" => ElementType.Link,
                _ => ElementType.Generic
            };

            elements[name] = new ElementInfo(selector, elementType);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"  Added: {name} ({elementType}) → {selector}");
            Console.ResetColor();
        }

        if (elements.Count == 0)
        {
            Console.WriteLine("No elements added. Exiting.");
            return 1;
        }

        var outputDir = "Generated";
        Directory.CreateDirectory(outputDir);

        var pageCode = TestGenerator.GeneratePageObject(pageName, urlPath, elements);
        var scenarios = new List<TestScenario>
        {
            new TestScenario
            {
                Name = $"{pageName}_ShouldLoad",
                Description = $"Verify {pageName} page loads successfully",
                Steps = new List<string>
                {
                    "var title = await _page.GetTitleAsync();",
                    "Assert.That(title, Is.Not.Empty, \"Page should have a title.\");"
                }
            }
        };
        var testCode = TestGenerator.GenerateTestClass(pageName, pageName, scenarios);

        var pageFile = Path.Combine(outputDir, $"{pageName}Page.cs");
        var testFile = Path.Combine(outputDir, $"{pageName}Tests.cs");

        await File.WriteAllTextAsync(pageFile, pageCode);
        await File.WriteAllTextAsync(testFile, testCode);

        PrintSuccess("Page Object", pageFile, pageCode);
        PrintSuccess("Test Class", testFile, testCode);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nFiles saved to: {Path.GetFullPath(outputDir)}/");
        Console.ResetColor();

        return 0;
    }

    static void ShowHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Quick generate:");
        Console.ResetColor();
        Console.WriteLine("    dotnet run -- generate \"<description>\" <url-path> [output-dir]");
        Console.WriteLine("    dotnet run -- generate \"Test login with email and password\" /login");
        Console.WriteLine("    dotnet run -- generate \"Search products and verify results\" /search ./MyTests");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Interactive mode:");
        Console.ResetColor();
        Console.WriteLine("    dotnet run -- interactive");
        Console.WriteLine("    dotnet run -- i");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Element-based (guided):");
        Console.ResetColor();
        Console.WriteLine("    dotnet run -- elements");
        Console.WriteLine("    Define page elements step-by-step with selectors and types.");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Examples:");
        Console.ResetColor();
        Console.WriteLine("    dotnet run -- generate \"Test the checkout form with shipping and payment\" /checkout");
        Console.WriteLine("    dotnet run -- generate \"Verify dashboard loads with charts and KPI widgets\" /dashboard");
        Console.WriteLine("    dotnet run -- generate \"Test user registration with validation errors\" /register");
    }

    static void PrintSuccess(string label, string filePath, string code)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"── {label}: {filePath} ──");
        Console.ResetColor();

        var lines = code.Split('\n');
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("///"))
                Console.ForegroundColor = ConsoleColor.DarkGreen;
            else if (line.Contains("class ") || line.Contains("namespace "))
                Console.ForegroundColor = ConsoleColor.Yellow;
            else if (line.Contains("async Task") || line.Contains("public "))
                Console.ForegroundColor = ConsoleColor.Cyan;
            else
                Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine(line);
        }
        Console.ResetColor();
    }

    static int HandleUnknown(string command)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Unknown command: '{command}'");
        Console.ResetColor();
        Console.WriteLine();
        ShowHelp();
        return 1;
    }

    static string InferPageName(string url)
    {
        var segments = url.Trim('/').Split('/');
        var last = segments.Length > 0 ? segments[^1] : "Generated";
        if (string.IsNullOrEmpty(last)) return "Generated";
        return char.ToUpper(last[0]) + last[1..].ToLower();
    }
}
