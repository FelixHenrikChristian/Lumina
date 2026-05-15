using Lumina.Core.Models;

namespace Lumina.Core.Services;

public sealed class JsonTagGroupStore : ITagGroupStore
{
    public const string FileName = "tag-groups.json";

    private readonly ISettingsStore _settingsStore;

    public JsonTagGroupStore()
        : this(new JsonSettingsStore())
    {
    }

    public JsonTagGroupStore(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public Task<IReadOnlyList<TagGroup>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return _settingsStore.LoadAsync<IReadOnlyList<TagGroup>>(
            FileName,
            [],
            cancellationToken);
    }

    public Task SaveAsync(
        IReadOnlyList<TagGroup> tagGroups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tagGroups);

        return _settingsStore.SaveAsync(
            FileName,
            tagGroups,
            cancellationToken);
    }
}
