using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.Core.Tests;

public sealed class JsonLocationStoreTests
{
    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsEmptyLocations()
    {
        var settingsStore = new JsonSettingsStore(CreateTemporaryDirectory());
        var locationStore = new JsonLocationStore(settingsStore);

        var locations = await locationStore.LoadAsync();

        Assert.Empty(locations);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsLocations()
    {
        var settingsStore = new JsonSettingsStore(CreateTemporaryDirectory());
        var locationStore = new JsonLocationStore(settingsStore);
        var expected = new[]
        {
            new Location
            {
                Id = "library",
                Name = "Library",
                Path = @"D:\Library",
            },
        };

        await locationStore.SaveAsync(expected);
        var actual = await locationStore.LoadAsync();

        var location = Assert.Single(actual);
        Assert.Equal("library", location.Id);
        Assert.Equal("Library", location.Name);
        Assert.Equal(@"D:\Library", location.Path);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Lumina.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
