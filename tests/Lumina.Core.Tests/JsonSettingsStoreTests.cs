using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.Core.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsFallbackValue()
    {
        var store = new JsonSettingsStore(CreateTemporaryDirectory());
        var fallback = new DisplaySettings { GridSize = 7 };

        var settings = await store.LoadAsync("settings.json", fallback);

        Assert.Equal(7, settings.GridSize);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsJson()
    {
        var store = new JsonSettingsStore(CreateTemporaryDirectory());
        var locations = new[]
        {
            new Location
            {
                Id = "main",
                Name = "Main",
                Path = @"D:\Media",
            },
        };

        await store.SaveAsync("locations.json", locations);
        var loaded = await store.LoadAsync<IReadOnlyList<Location>>("locations.json", []);

        var location = Assert.Single(loaded);
        Assert.Equal("main", location.Id);
        Assert.Equal("Main", location.Name);
        Assert.Equal(@"D:\Media", location.Path);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Lumina.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
