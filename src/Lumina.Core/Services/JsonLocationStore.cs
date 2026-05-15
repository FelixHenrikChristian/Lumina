using Lumina.Core.Models;

namespace Lumina.Core.Services;

public sealed class JsonLocationStore : ILocationStore
{
    public const string FileName = "locations.json";

    private readonly ISettingsStore _settingsStore;

    public JsonLocationStore()
        : this(new JsonSettingsStore())
    {
    }

    public JsonLocationStore(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public Task<IReadOnlyList<Location>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return _settingsStore.LoadAsync<IReadOnlyList<Location>>(
            FileName,
            [],
            cancellationToken);
    }

    public Task SaveAsync(
        IReadOnlyList<Location> locations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(locations);

        return _settingsStore.SaveAsync(
            FileName,
            locations,
            cancellationToken);
    }
}
