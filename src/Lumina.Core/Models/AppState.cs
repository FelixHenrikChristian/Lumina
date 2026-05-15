namespace Lumina.Core.Models;

public sealed record AppState
{
    public string? SelectedLocationId { get; init; }

    public string? SelectedTagId { get; init; }
}
