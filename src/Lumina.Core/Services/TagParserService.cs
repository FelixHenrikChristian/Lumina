using System.Text.RegularExpressions;

namespace Lumina.Core.Services;

public sealed partial class TagParserService : ITagParserService
{
    public IReadOnlyList<string> ParseTagsFromFilename(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);

        var match = LeadingTagsRegex().Match(filename);
        if (!match.Success)
        {
            return [];
        }

        return match.Groups["tags"]
            .Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public string GetDisplayName(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);

        return LeadingTagsRegex().Replace(filename, string.Empty, 1).TrimStart();
    }

    public string GetDisplayNameWithoutExtension(string filename)
    {
        var displayName = GetDisplayName(filename);
        var extension = Path.GetExtension(displayName);
        return extension.Length == 0
            ? displayName
            : displayName[..^extension.Length];
    }

    public string InsertTagIntoFilename(string filename, string tag, int insertionIndex)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var normalizedTag = tag.Trim();
        var tags = ParseTagsFromFilename(filename)
            .Where(existingTag => !string.Equals(
                existingTag,
                normalizedTag,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var targetIndex = Math.Clamp(insertionIndex, 0, tags.Count);
        tags.Insert(targetIndex, normalizedTag);

        var displayName = GetDisplayName(filename);
        return $"[{string.Join(' ', tags)}] {displayName}";
    }

    public string RemoveTagFromFilename(string filename, string tag)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var normalizedTag = tag.Trim();
        var tags = ParseTagsFromFilename(filename);
        if (!tags.Any(existingTag => string.Equals(
                existingTag,
                normalizedTag,
                StringComparison.OrdinalIgnoreCase)))
        {
            return filename;
        }

        var remainingTags = tags
            .Where(existingTag => !string.Equals(
                existingTag,
                normalizedTag,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var displayName = GetDisplayName(filename);

        return remainingTags.Count == 0
            ? displayName
            : $"[{string.Join(' ', remainingTags)}] {displayName}";
    }

    [GeneratedRegex(@"^\[(?<tags>[^\]]+)\]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingTagsRegex();
}
