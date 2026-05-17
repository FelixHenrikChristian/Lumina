using Lumina.Core.Services;

namespace Lumina.Core.Tests;

public sealed class FileSystemBrowserServiceTests : IDisposable
{
    private readonly string _temporaryDirectory = CreateTemporaryDirectory();
    private readonly FileSystemBrowserService _service = new();

    [Fact]
    public async Task LoadDirectoryAsync_ReturnsImmediateChildrenWithFoldersFirst()
    {
        Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Zeta Folder"));
        Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Alpha Folder"));
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "alpha.txt"), "abc");
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "[work urgent] Zebra.txt"), "tagged");

        var items = await _service.LoadDirectoryAsync(_temporaryDirectory);

        Assert.Equal(
            ["Alpha Folder", "Zeta Folder", "alpha.txt", "[work urgent] Zebra.txt"],
            items.Select(item => item.Name));

        Assert.True(items[0].IsDirectory);
        Assert.True(items[1].IsDirectory);
        Assert.False(items[2].IsDirectory);
        Assert.False(items[3].IsDirectory);
    }

    [Fact]
    public async Task LoadDirectoryAsync_ParsesTagsAndDisplayNameFromFileName()
    {
        var filePath = Path.Combine(_temporaryDirectory, "[work urgent] Zebra.txt");
        await File.WriteAllTextAsync(filePath, "tagged");

        var items = await _service.LoadDirectoryAsync(_temporaryDirectory);

        var item = Assert.Single(items);
        Assert.Equal("[work urgent] Zebra.txt", item.Name);
        Assert.Equal("Zebra.txt", item.DisplayName);
        Assert.Equal(["work", "urgent"], item.Tags);
        Assert.Equal(filePath, item.Path);
        Assert.Equal(new FileInfo(filePath).Length, item.Size);
    }

    [Fact]
    public async Task LoadDirectoryAsync_DoesNotRecurseIntoChildFolders()
    {
        var childDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Child"));
        await File.WriteAllTextAsync(Path.Combine(childDirectory.FullName, "nested.txt"), "nested");

        var items = await _service.LoadDirectoryAsync(_temporaryDirectory);

        var item = Assert.Single(items);
        Assert.Equal("Child", item.Name);
        Assert.True(item.IsDirectory);
    }

    [Fact]
    public async Task LoadDirectoryAsync_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        var missingDirectory = Path.Combine(_temporaryDirectory, "missing");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _service.LoadDirectoryAsync(missingDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Lumina-Core-Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        return path;
    }
}
