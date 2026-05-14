namespace Lumina.Core.Models;

public sealed record Tag
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = string.Empty;

    public string Color { get; init; } = "#2196f3";

    public string? TextColor { get; init; }

    public string? GroupId { get; init; }
}
