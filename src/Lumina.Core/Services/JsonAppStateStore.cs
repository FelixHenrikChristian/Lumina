using Lumina.Core.Models;

namespace Lumina.Core.Services;

public sealed class JsonAppStateStore : IAppStateStore
{
    public const string FileName = "app-state.json";

    private readonly ISettingsStore _settingsStore;

    public JsonAppStateStore()
        : this(new JsonSettingsStore())
    {
    }

    public JsonAppStateStore(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public Task<AppState> LoadAsync(CancellationToken cancellationToken = default)
    {
        return _settingsStore.LoadAsync(
            FileName,
            new AppState(),
            cancellationToken);
    }

    public Task SaveAsync(AppState appState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appState);

        return _settingsStore.SaveAsync(
            FileName,
            appState,
            cancellationToken);
    }
}
