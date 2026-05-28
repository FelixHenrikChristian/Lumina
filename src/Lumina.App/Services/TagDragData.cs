using System.Text.Json;

using Windows.ApplicationModel.DataTransfer;

namespace Lumina.App.Services;

public static class TagDragData
{
    public const string FormatId = "Lumina.TagLibrary.Tag";

    public static DraggedTag? Current { get; private set; }

    public static void SetData(DataPackage dataPackage, DraggedTag tag)
    {
        ArgumentNullException.ThrowIfNull(dataPackage);
        ArgumentNullException.ThrowIfNull(tag);

        Current = tag;
        dataPackage.SetData(FormatId, JsonSerializer.Serialize(tag));
    }

    public static void Clear()
    {
        Current = null;
    }

    public static bool Contains(DataPackageView dataView)
    {
        ArgumentNullException.ThrowIfNull(dataView);

        return Current is not null || dataView.Contains(FormatId);
    }

    public static bool TryGetCurrent(out DraggedTag tag)
    {
        if (Current is null)
        {
            tag = default!;
            return false;
        }

        tag = Current;
        return true;
    }

    public static async Task<DraggedTag?> GetDroppedTagAsync(DataPackageView dataView)
    {
        ArgumentNullException.ThrowIfNull(dataView);

        if (Current is not null)
        {
            return Current;
        }

        if (!dataView.Contains(FormatId))
        {
            return null;
        }

        var data = await dataView.GetDataAsync(FormatId);
        return data is string json
            ? JsonSerializer.Deserialize<DraggedTag>(json)
            : null;
    }
}

public sealed record DraggedTag(
    string Id,
    string Name,
    string Color,
    string TextColor);
