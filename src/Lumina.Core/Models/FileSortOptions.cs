namespace Lumina.Core.Models;

public sealed record FileSortOptions(
    FileSortField Field,
    FileSortDirection Direction)
{
    public static FileSortOptions Default { get; } = new(
        FileSortField.Name,
        FileSortDirection.Ascending);
}
