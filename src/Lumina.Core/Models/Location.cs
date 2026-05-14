namespace Lumina.Core.Models;

public sealed record Location
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
}
