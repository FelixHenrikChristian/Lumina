namespace Lumina.Core.Models;

public sealed record TagGroup
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = string.Empty;

    public string DefaultColor { get; init; } = "#2196f3";

    public string? DefaultTextColor { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<Tag> Tags { get; init; } = [];
}
