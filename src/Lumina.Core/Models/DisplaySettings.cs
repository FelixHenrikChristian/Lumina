namespace Lumina.Core.Models;

public sealed record DisplaySettings
{
    public bool ShowFolderNameInIcon { get; init; }

    public bool EnableSimplifiedTraditionalSearch { get; init; }

    public bool NavigateToTargetAfterOperation { get; init; }

    public bool HideFileExtension { get; init; }

    public bool ShowParentFolderInRecursiveSearch { get; init; } = true;

    public int GridSize { get; init; } = 6;

    public string SidebarView { get; init; } = "locations";

    public string TagDisplayStyle { get; init; } = "original";

    public bool GroupSortLocked { get; init; }
}
