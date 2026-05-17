namespace Lumina.Core.Models;

public sealed record FileItem
{
    public string Name { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public long Size { get; init; }

    public DateTimeOffset Modified { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
