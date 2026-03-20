using System.Text.Json;
using Microsoft.Playwright;
using PlaywrightFramework.Config;

namespace PlaywrightFramework.Core;

/// <summary>
/// AI-assisted self-healing locator wrapper. When a primary locator fails,
/// it automatically tries alternative strategies (data-testid, aria-label,
/// text content, role, CSS) and logs the healed locator for maintenance.
/// </summary>
public sealed class SelfHealingLocator
{
    private readonly IPage _page;
    private readonly SelfHealingSettings _settings;
    private static readonly List<HealingRecord> _healingLog = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes a new self-healing locator bound to a page.
    /// </summary>
    /// <param name="page">The Playwright page instance.</param>
    /// <param name="settings">Self-healing configuration.</param>
    public SelfHealingLocator(IPage page, SelfHealingSettings settings)
    {
        _page = page;
        _settings = settings;
    }

    /// <summary>
    /// Finds an element using the primary locator. If it fails, attempts
    /// alternative strategies to locate the element automatically.
    /// </summary>
    /// <param name="primaryLocator">The original CSS/XPath selector.</param>
    /// <param name="friendlyName">Human-readable name for logging (e.g., "Login Button").</param>
    /// <param name="alternatives">Optional explicit alternative locators to try.</param>
    /// <returns>A Playwright locator that resolved successfully.</returns>
    /// <exception cref="SelfHealingException">Thrown when all strategies fail.</exception>
    public async Task<ILocator> FindAsync(
        string primaryLocator,
        string friendlyName = "",
        AlternativeLocators? alternatives = null)
    {
        // 1. Try the primary locator first
        try
        {
            var locator = _page.Locator(primaryLocator);
            await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            return locator;
        }
        catch (TimeoutException)
        {
            if (!_settings.Enabled)
                throw;

            Console.WriteLine($"[SelfHealing] Primary locator failed: '{primaryLocator}' ({friendlyName}). Attempting healing...");
        }

        // 2. Build candidate strategies
        var candidates = BuildCandidates(primaryLocator, alternatives);

        // 3. Try each candidate
        foreach (var (strategy, selector) in candidates)
        {
            try
            {
                var locator = _page.Locator(selector);
                var count = await locator.CountAsync();
                if (count > 0)
                {
                    await locator.First.WaitForAsync(new()
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = 3000
                    });

                    LogHealing(primaryLocator, selector, strategy, friendlyName);
                    Console.WriteLine($"[SelfHealing] HEALED '{friendlyName}' using {strategy}: '{selector}'");
                    return locator.First;
                }
            }
            catch
            {
                // Strategy failed, try next
            }
        }

        // 4. Last resort: search the full DOM for elements with matching text
        if (!string.IsNullOrEmpty(friendlyName))
        {
            try
            {
                var textLocator = _page.GetByText(friendlyName, new() { Exact = false });
                if (await textLocator.CountAsync() > 0)
                {
                    LogHealing(primaryLocator, $"GetByText('{friendlyName}')", "TextContent", friendlyName);
                    Console.WriteLine($"[SelfHealing] HEALED '{friendlyName}' using text content match.");
                    return textLocator.First;
                }
            }
            catch
            {
                // Text search also failed
            }
        }

        throw new SelfHealingException(
            $"Self-healing failed for '{friendlyName}'. Primary: '{primaryLocator}'. " +
            $"Tried {candidates.Count} alternative strategies. Please update the locator manually.");
    }

    /// <summary>
    /// Clicks an element using self-healing locator resolution.
    /// </summary>
    public async Task ClickAsync(string primaryLocator, string friendlyName = "", AlternativeLocators? alternatives = null)
    {
        var locator = await FindAsync(primaryLocator, friendlyName, alternatives);
        await locator.ClickAsync();
    }

    /// <summary>
    /// Fills a text input using self-healing locator resolution.
    /// </summary>
    public async Task FillAsync(string primaryLocator, string value, string friendlyName = "", AlternativeLocators? alternatives = null)
    {
        var locator = await FindAsync(primaryLocator, friendlyName, alternatives);
        await locator.FillAsync(value);
    }

    /// <summary>
    /// Gets the inner text of an element using self-healing locator resolution.
    /// </summary>
    public async Task<string> InnerTextAsync(string primaryLocator, string friendlyName = "", AlternativeLocators? alternatives = null)
    {
        var locator = await FindAsync(primaryLocator, friendlyName, alternatives);
        return await locator.InnerTextAsync();
    }

    /// <summary>
    /// Checks if an element is visible using self-healing locator resolution.
    /// </summary>
    public async Task<bool> IsVisibleAsync(string primaryLocator, string friendlyName = "", AlternativeLocators? alternatives = null)
    {
        try
        {
            var locator = await FindAsync(primaryLocator, friendlyName, alternatives);
            return await locator.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Saves the healing report to disk as JSON for developer review.
    /// Call this at the end of a test run to persist healed locator records.
    /// </summary>
    public static async Task SaveHealingReportAsync(string path)
    {
        List<HealingRecord> snapshot;
        lock (_lock)
        {
            snapshot = new List<HealingRecord>(_healingLog);
        }

        if (snapshot.Count == 0) return;

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);

        Console.WriteLine($"[SelfHealing] Healing report saved: {path} ({snapshot.Count} records)");
    }

    /// <summary>
    /// Clears the in-memory healing log. Call between test runs if needed.
    /// </summary>
    public static void ClearHealingLog()
    {
        lock (_lock) { _healingLog.Clear(); }
    }

    #region Private helpers

    private List<(string Strategy, string Selector)> BuildCandidates(
        string primary, AlternativeLocators? alt)
    {
        var candidates = new List<(string, string)>();

        // Explicit alternatives provided by the page object
        if (alt != null)
        {
            if (!string.IsNullOrEmpty(alt.DataTestId))
                candidates.Add(("data-testid", $"[data-testid='{alt.DataTestId}']"));
            if (!string.IsNullOrEmpty(alt.AriaLabel))
                candidates.Add(("aria-label", $"[aria-label='{alt.AriaLabel}']"));
            if (!string.IsNullOrEmpty(alt.Text))
                candidates.Add(("text", $"text={alt.Text}"));
            if (!string.IsNullOrEmpty(alt.Role))
                candidates.Add(("role", alt.Role));
            if (!string.IsNullOrEmpty(alt.CssSelector))
                candidates.Add(("css", alt.CssSelector));
            if (!string.IsNullOrEmpty(alt.XPath))
                candidates.Add(("xpath", alt.XPath));
        }

        // Auto-derived alternatives from the primary selector
        candidates.AddRange(DeriveAlternatives(primary));

        return candidates;
    }

    private static List<(string Strategy, string Selector)> DeriveAlternatives(string primary)
    {
        var derived = new List<(string, string)>();

        // If primary is an ID selector like #username, try name, data-testid, aria-label
        if (primary.StartsWith('#'))
        {
            var id = primary.TrimStart('#');
            derived.Add(("id-attr", $"[id='{id}']"));
            derived.Add(("name-attr", $"[name='{id}']"));
            derived.Add(("data-testid-derived", $"[data-testid='{id}']"));
            derived.Add(("aria-label-derived", $"[aria-label*='{id}']"));
            derived.Add(("placeholder-derived", $"[placeholder*='{id}']"));
        }

        // If primary contains a class selector, try partial matches
        if (primary.Contains('.') && !primary.Contains('/'))
        {
            var parts = primary.Split('.');
            if (parts.Length >= 2)
            {
                var className = parts[^1].Split(new[] { ' ', '>', '+', '~' })[0];
                if (!string.IsNullOrEmpty(className))
                {
                    derived.Add(("class-contains", $"[class*='{className}']"));
                }
            }
        }

        // If primary looks like an XPath, try CSS equivalent patterns
        if (primary.StartsWith("//"))
        {
            // Extract element type and any text content hints
            var match = System.Text.RegularExpressions.Regex.Match(
                primary, @"//(\w+)(?:\[.*?['""](.+?)['""].*?\])?");
            if (match.Success)
            {
                var tag = match.Groups[1].Value;
                var hint = match.Groups[2].Value;
                if (!string.IsNullOrEmpty(hint))
                {
                    derived.Add(("xpath-to-text", $"{tag}:has-text('{hint}')"));
                    derived.Add(("xpath-to-contains", $"text={hint}"));
                }
            }
        }

        return derived;
    }

    private void LogHealing(string original, string healed, string strategy, string friendlyName)
    {
        if (!_settings.LogHealedLocators) return;

        var record = new HealingRecord
        {
            Timestamp = DateTime.UtcNow,
            FriendlyName = friendlyName,
            OriginalLocator = original,
            HealedLocator = healed,
            Strategy = strategy,
            PageUrl = _page.Url
        };

        lock (_lock) { _healingLog.Add(record); }
    }

    #endregion
}

/// <summary>
/// Alternative locator strategies for self-healing fallback.
/// Provide as many as possible in page objects for robust healing.
/// </summary>
public sealed class AlternativeLocators
{
    public string? DataTestId { get; set; }
    public string? AriaLabel { get; set; }
    public string? Text { get; set; }
    public string? Role { get; set; }
    public string? CssSelector { get; set; }
    public string? XPath { get; set; }
}

/// <summary>
/// Thrown when self-healing exhausts all alternative strategies.
/// </summary>
public sealed class SelfHealingException : Exception
{
    public SelfHealingException(string message) : base(message) { }
    public SelfHealingException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// A single record of a locator that was healed at runtime.
/// </summary>
public sealed class HealingRecord
{
    public DateTime Timestamp { get; set; }
    public string FriendlyName { get; set; } = string.Empty;
    public string OriginalLocator { get; set; } = string.Empty;
    public string HealedLocator { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public string PageUrl { get; set; } = string.Empty;
}
