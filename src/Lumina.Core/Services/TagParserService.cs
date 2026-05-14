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

    [GeneratedRegex(@"^\[(?<tags>[^\]]+)\]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingTagsRegex();
}
