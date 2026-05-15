namespace Lumina.Core.Models;

public sealed record TagGroupImportResult
{
    public string SourceFormat { get; init; } = string.Empty;

    public int TagGroupCount { get; init; }

    public int TagCount { get; init; }
}
