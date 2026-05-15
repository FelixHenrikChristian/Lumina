using System.Text.Json;

using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.Core.Tests;

public sealed class JsonTagGroupTransferServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesPortableTagLibraryFile()
    {
        var settingsStore = new JsonSettingsStore(CreateTemporaryDirectory());
        var service = CreateService(settingsStore);
        var exportPath = Path.Combine(settingsStore.AppDataDirectory, "lumina-tags.json");

        await new JsonTagGroupStore(settingsStore).SaveAsync(
        [
            new TagGroup
            {
                Id = "people",
                Name = "People",
                DefaultColor = "#2196f3",
                DefaultTextColor = "#ffffff",
                Description = "People tags",
                Tags =
                [
                    new Tag
                    {
                        Id = "alice",
                        Name = "Alice",
                        Color = "#f44336",
                        TextColor = "#ffffff",
                        GroupId = "people",
                    },
                ],
            },
        ]);

        await service.ExportAsync(exportPath);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(exportPath));
        var root = document.RootElement;
        Assert.Equal("Lumina", root.GetProperty("appName").GetString());
        Assert.Equal(1, root.GetProperty("settingsVersion").GetInt32());
        Assert.False(root.TryGetProperty("locations", out _));
        Assert.False(root.TryGetProperty("appState", out _));

        var group = root.GetProperty("tagGroups")[0];
        Assert.Equal("People", group.GetProperty("title").GetString());
        Assert.Equal("people", group.GetProperty("uuid").GetString());
        Assert.True(group.TryGetProperty("created_date", out _));
        Assert.Equal("Alice", group.GetProperty("children")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task ImportAsync_TagAnythingLibrary_OverwritesOnlyTagGroups()
    {
        var settingsStore = new JsonSettingsStore(CreateTemporaryDirectory());
        var service = CreateService(settingsStore);
        var importPath = Path.Combine(settingsStore.AppDataDirectory, "tag-anything.json");
        await new JsonLocationStore(settingsStore).SaveAsync(
        [
            new Location
            {
                Id = "existing",
                Name = "Existing",
                Path = @"D:\Existing",
            },
        ]);
        await new JsonAppStateStore(settingsStore).SaveAsync(new AppState
        {
            SelectedLocationId = "existing",
            SelectedTagId = "old",
        });
        var json = """
            {
              "appName": "TagAnything",
              "appVersion": "1.0.0",
              "settingsVersion": 3,
              "tagGroups": [
                {
                  "title": "People",
                  "uuid": "people",
                  "children": [
                    {
                      "title": "Alice",
                      "color": "#f44336",
                      "textcolor": "#ffffff"
                    }
                  ],
                  "created_date": 1,
                  "color": "#2196f3",
                  "textcolor": "#ffffff",
                  "modified_date": 2,
                  "expanded": true
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(importPath, json);

        var result = await service.ImportAsync(importPath);

        Assert.Equal("TagAnything", result.SourceFormat);
        Assert.Equal(1, result.TagGroupCount);
        Assert.Equal(1, result.TagCount);

        var location = Assert.Single(await new JsonLocationStore(settingsStore).LoadAsync());
        Assert.Equal("existing", location.Id);

        var appState = await new JsonAppStateStore(settingsStore).LoadAsync();
        Assert.Equal("existing", appState.SelectedLocationId);
        Assert.Equal("old", appState.SelectedTagId);

        var group = Assert.Single(await new JsonTagGroupStore(settingsStore).LoadAsync());
        Assert.Equal("people", group.Id);
        Assert.Equal("People", group.Name);
        var tag = Assert.Single(group.Tags);
        Assert.Equal("Alice", tag.Name);
        Assert.Equal("people", tag.GroupId);
    }

    [Fact]
    public async Task ImportAsync_NativeTagGroupArray_OverwritesTagGroups()
    {
        var settingsStore = new JsonSettingsStore(CreateTemporaryDirectory());
        var service = CreateService(settingsStore);
        var importPath = Path.Combine(settingsStore.AppDataDirectory, "native-tags.json");
        var json = """
            [
              {
                "id": "status",
                "name": "Status",
                "defaultColor": "#2196f3",
                "defaultTextColor": "#ffffff",
                "tags": [
                  {
                    "id": "done",
                    "name": "Done",
                    "color": "#4caf50",
                    "textColor": "#ffffff",
                    "groupId": "status"
                  }
                ]
              }
            ]
            """;
        await File.WriteAllTextAsync(importPath, json);

        var result = await service.ImportAsync(importPath);

        Assert.Equal("Lumina tag groups", result.SourceFormat);
        Assert.Equal(1, result.TagGroupCount);
        Assert.Equal(1, result.TagCount);

        var group = Assert.Single(await new JsonTagGroupStore(settingsStore).LoadAsync());
        Assert.Equal("status", group.Id);
        Assert.Equal("Status", group.Name);
        Assert.Equal("Done", Assert.Single(group.Tags).Name);
    }

    private static JsonTagGroupTransferService CreateService(JsonSettingsStore settingsStore)
    {
        return new JsonTagGroupTransferService(new JsonTagGroupStore(settingsStore));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Lumina.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
