namespace Lumina.Core.Models;

public sealed record FileOperationEntry
{
    public FileOperationEntryKind Kind { get; init; }

    public string SourcePath { get; init; } = string.Empty;

    public string DestinationPath { get; init; } = string.Empty;

    public string BackupPath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }
}
