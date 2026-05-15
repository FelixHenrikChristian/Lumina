using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.Core.Tests;

public sealed class JsonTagGroupStoreTests
{
    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsEmptyTagGroups()
    {
        var settingsStore = new JsonSettingsStore(CreateTemporaryDirectory());
        var tagGroupStore = new JsonTagGroupStore(settingsStore);

        var tagGroups = await tagGroupStore.LoadAsync();

        Assert.Empty(tagGroups);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsTagGroups()
    {
        var settingsStore = new JsonSettingsStore(CreateTemporaryDirectory());
        var tagGroupStore = new JsonTagGroupStore(settingsStore);
        var expected = new[]
        {
            new TagGroup
            {
                Id = "work",
                Name = "Work",
                DefaultColor = "#2196f3",
                DefaultTextColor = "#ffffff",
                Description = "Work tags",
                Tags =
                [
                    new Tag
                    {
                        Id = "tag-work",
                        Name = "work",
                        Color = "#4caf50",
                        TextColor = "#ffffff",
                        GroupId = "work",
                    },
                ],
            },
        };

        await tagGroupStore.SaveAsync(expected);
        var actual = await tagGroupStore.LoadAsync();

        var group = Assert.Single(actual);
        Assert.Equal("work", group.Id);
        Assert.Equal("Work", group.Name);
        Assert.Equal("#2196f3", group.DefaultColor);
        Assert.Equal("#ffffff", group.DefaultTextColor);
        Assert.Equal("Work tags", group.Description);

        var tag = Assert.Single(group.Tags);
        Assert.Equal("tag-work", tag.Id);
        Assert.Equal("work", tag.Name);
        Assert.Equal("#4caf50", tag.Color);
        Assert.Equal("#ffffff", tag.TextColor);
        Assert.Equal("work", tag.GroupId);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Lumina.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
