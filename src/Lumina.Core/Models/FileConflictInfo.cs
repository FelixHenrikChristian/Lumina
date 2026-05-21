namespace Lumina.Core.Models;

public sealed record FileConflictInfo
{
    public FileOperationKind Operation { get; init; }

    public string SourcePath { get; init; } = string.Empty;

    public string DestinationPath { get; init; } = string.Empty;

    public bool SourceIsDirectory { get; init; }

    public bool DestinationIsDirectory { get; init; }

    public string Name => Path.GetFileName(
        DestinationPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));
}
