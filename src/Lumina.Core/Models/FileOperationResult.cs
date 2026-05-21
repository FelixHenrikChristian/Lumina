namespace Lumina.Core.Models;

public sealed record FileOperationResult
{
    public FileOperationKind Operation { get; init; }

    public IReadOnlyList<string> Paths { get; init; } = [];

    public IReadOnlyList<FileOperationEntry> Entries { get; init; } = [];
}
