using Lumina.Core.Models;
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

    [Fact]
    public async Task SearchDirectoryAsync_FindsFilesByDisplayNameTagAndNestedName()
    {
        var childDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Child"));
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "[work urgent] Zebra.txt"), "tagged");
        await File.WriteAllTextAsync(Path.Combine(childDirectory.FullName, "nested-note.txt"), "nested");

        var tagMatches = await _service.SearchDirectoryAsync(_temporaryDirectory, "urgent");
        var nestedMatches = await _service.SearchDirectoryAsync(_temporaryDirectory, "nested");

        var tagMatch = Assert.Single(tagMatches);
        Assert.Equal("[work urgent] Zebra.txt", tagMatch.Name);

        var nestedMatch = Assert.Single(nestedMatches);
        Assert.Equal("nested-note.txt", nestedMatch.Name);
        Assert.Equal(Path.Combine(childDirectory.FullName, "nested-note.txt"), nestedMatch.Path);
    }

    [Fact]
    public async Task RenameAsync_RenamesFileInPlace()
    {
        var filePath = Path.Combine(_temporaryDirectory, "old.txt");
        await File.WriteAllTextAsync(filePath, "content");

        var renamedPath = await _service.RenameAsync(filePath, "new.txt");

        Assert.Equal(Path.Combine(_temporaryDirectory, "new.txt"), renamedPath);
        Assert.False(File.Exists(filePath));
        Assert.Equal("content", await File.ReadAllTextAsync(renamedPath));
    }

    [Fact]
    public async Task RenameAsync_InvalidName_ThrowsArgumentException()
    {
        var filePath = Path.Combine(_temporaryDirectory, "old.txt");
        await File.WriteAllTextAsync(filePath, "content");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RenameAsync(filePath, Path.Combine("other", "new.txt")));
    }

    [Fact]
    public async Task DeleteAsync_PermanentDeletesFilesAndDirectories()
    {
        var filePath = Path.Combine(_temporaryDirectory, "delete.txt");
        var directoryPath = Path.Combine(_temporaryDirectory, "Delete Folder");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllTextAsync(filePath, "delete");
        await File.WriteAllTextAsync(Path.Combine(directoryPath, "child.txt"), "child");

        await _service.DeleteAsync(
            [filePath, directoryPath],
            FileDeleteBehavior.Permanent);

        Assert.False(File.Exists(filePath));
        Assert.False(Directory.Exists(directoryPath));
    }

    [Fact]
    public async Task CopyAsync_CopiesFileToDestinationDirectory()
    {
        var sourcePath = Path.Combine(_temporaryDirectory, "source.txt");
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Destination"));
        await File.WriteAllTextAsync(sourcePath, "copy");

        var copiedPaths = await _service.CopyAsync([sourcePath], destinationDirectory.FullName);

        var copiedPath = Assert.Single(copiedPaths);
        Assert.Equal(Path.Combine(destinationDirectory.FullName, "source.txt"), copiedPath);
        Assert.True(File.Exists(sourcePath));
        Assert.Equal("copy", await File.ReadAllTextAsync(copiedPath));
    }

    [Fact]
    public async Task CopyAsync_SameDirectoryUsesExplorerStyleCopyName()
    {
        var sourcePath = Path.Combine(_temporaryDirectory, "source.txt");
        await File.WriteAllTextAsync(sourcePath, "copy");

        var copiedPaths = await _service.CopyAsync([sourcePath], _temporaryDirectory);

        var copiedPath = Assert.Single(copiedPaths);
        Assert.Equal(Path.Combine(_temporaryDirectory, "source - Copy.txt"), copiedPath);
        Assert.True(File.Exists(sourcePath));
        Assert.Equal("copy", await File.ReadAllTextAsync(copiedPath));
    }

    [Fact]
    public async Task CopyAsync_CopiesDirectoryTree()
    {
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Source Folder"));
        var nestedDirectory = Directory.CreateDirectory(Path.Combine(sourceDirectory.FullName, "Nested"));
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Destination"));
        await File.WriteAllTextAsync(Path.Combine(nestedDirectory.FullName, "child.txt"), "child");

        var copiedPaths = await _service.CopyAsync([sourceDirectory.FullName], destinationDirectory.FullName);

        var copiedPath = Assert.Single(copiedPaths);
        Assert.Equal(Path.Combine(destinationDirectory.FullName, "Source Folder"), copiedPath);
        Assert.Equal(
            "child",
            await File.ReadAllTextAsync(Path.Combine(copiedPath, "Nested", "child.txt")));
    }

    [Fact]
    public async Task MoveAsync_MovesFileToDestinationDirectory()
    {
        var sourcePath = Path.Combine(_temporaryDirectory, "source.txt");
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Destination"));
        await File.WriteAllTextAsync(sourcePath, "move");

        var movedPaths = await _service.MoveAsync([sourcePath], destinationDirectory.FullName);

        var movedPath = Assert.Single(movedPaths);
        Assert.Equal(Path.Combine(destinationDirectory.FullName, "source.txt"), movedPath);
        Assert.False(File.Exists(sourcePath));
        Assert.Equal("move", await File.ReadAllTextAsync(movedPath));
    }

    [Fact]
    public async Task MoveAsync_DestinationExists_ThrowsIOException()
    {
        var sourcePath = Path.Combine(_temporaryDirectory, "source.txt");
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Destination"));
        await File.WriteAllTextAsync(sourcePath, "source");
        await File.WriteAllTextAsync(Path.Combine(destinationDirectory.FullName, "source.txt"), "existing");

        await Assert.ThrowsAsync<IOException>(
            () => _service.MoveAsync([sourcePath], destinationDirectory.FullName));
        Assert.True(File.Exists(sourcePath));
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
