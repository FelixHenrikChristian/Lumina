namespace Lumina.Core.Models;

public sealed record FileOperationProgress
{
    public FileOperationKind Operation { get; init; }

    public FileOperationProgressStage Stage { get; init; }

    public string SourcePath { get; init; } = string.Empty;

    public string DestinationPath { get; init; } = string.Empty;

    public string CurrentItemName { get; init; } = string.Empty;

    public int CompletedItems { get; init; }

    public int TotalItems { get; init; }

    public long CompletedBytes { get; init; }

    public long TotalBytes { get; init; }

    public double PercentComplete
    {
        get
        {
            if (Stage == FileOperationProgressStage.Completed)
            {
                return 100;
            }

            if (TotalBytes > 0)
            {
                return Math.Clamp((double)CompletedBytes / TotalBytes * 100, 0, 100);
            }

            if (TotalItems > 0)
            {
                return Math.Clamp((double)CompletedItems / TotalItems * 100, 0, 100);
            }

            return 0;
        }
    }
}
