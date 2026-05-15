using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.Core.Tests;

public sealed class JsonAppStateStoreTests
{
    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsEmptyAppState()
    {
        var settingsStore = new JsonSettingsStore(CreateTemporaryDirectory());
        var appStateStore = new JsonAppStateStore(settingsStore);

        var appState = await appStateStore.LoadAsync();

        Assert.Null(appState.SelectedLocationId);
        Assert.Null(appState.SelectedTagId);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsAppState()
    {
        var settingsStore = new JsonSettingsStore(CreateTemporaryDirectory());
        var appStateStore = new JsonAppStateStore(settingsStore);
        var expected = new AppState
        {
            SelectedLocationId = "downloads",
            SelectedTagId = "favorites",
        };

        await appStateStore.SaveAsync(expected);
        var actual = await appStateStore.LoadAsync();

        Assert.Equal("downloads", actual.SelectedLocationId);
        Assert.Equal("favorites", actual.SelectedTagId);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Lumina.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
