namespace Lumina.Core.Models;

public static class DisplayLanguage
{
    public const string System = "system";
    public const string English = "en-US";
    public const string SimplifiedChinese = "zh-Hans";

    public static string Normalize(string? language)
    {
        var normalized = language?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return System;
        }

        if (string.Equals(normalized, System, StringComparison.OrdinalIgnoreCase))
        {
            return System;
        }

        if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return SimplifiedChinese;
        }

        if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        return System;
    }
}
