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
        Assert.Equal(
            new DateTimeOffset(File.GetCreationTimeUtc(filePath), TimeSpan.Zero),
            item.Created);
    }

    [Fact]
    public async Task LoadDirectoryAsync_SortBySizeDescendingKeepsFoldersFirst()
    {
        Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Folder"));
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "small.bin"), "1");
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "large.bin"), "12345");

        var items = await _service.LoadDirectoryAsync(
            _temporaryDirectory,
            new FileSortOptions(FileSortField.Size, FileSortDirection.Descending));

        Assert.Equal(
            ["Folder", "large.bin", "small.bin"],
            items.Select(item => item.Name));
    }

    [Fact]
    public async Task LoadDirectoryAsync_SortByTypeUsesNameAsTieBreaker()
    {
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "[tag] zeta.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "alpha.jpg"), "a");
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "beta.txt"), "a");

        var items = await _service.LoadDirectoryAsync(
            _temporaryDirectory,
            new FileSortOptions(FileSortField.Type, FileSortDirection.Ascending));

        Assert.Equal(
            ["alpha.jpg", "beta.txt", "[tag] zeta.txt"],
            items.Select(item => item.Name));
    }

    [Fact]
    public async Task LoadDirectoryAsync_SortByModifiedDateDescending()
    {
        var olderPath = Path.Combine(_temporaryDirectory, "older.txt");
        var newerPath = Path.Combine(_temporaryDirectory, "newer.txt");
        await File.WriteAllTextAsync(olderPath, "older");
        await File.WriteAllTextAsync(newerPath, "newer");
        File.SetLastWriteTimeUtc(olderPath, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newerPath, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var items = await _service.LoadDirectoryAsync(
            _temporaryDirectory,
            new FileSortOptions(FileSortField.Modified, FileSortDirection.Descending));

        Assert.Equal(
            ["newer.txt", "older.txt"],
            items.Select(item => item.Name));
    }

    [Fact]
    public async Task LoadDirectoryAsync_AssignsPreviewKindForImageAndVideoFiles()
    {
        Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Media Folder"));
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "notes.txt"), "notes");
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "photo.JPG"), "image");
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "video.MP4"), "video");

        var items = await _service.LoadDirectoryAsync(_temporaryDirectory);

        Assert.Equal(FilePreviewKind.None, items.Single(item => item.Name == "Media Folder").PreviewKind);
        Assert.Equal(FilePreviewKind.None, items.Single(item => item.Name == "notes.txt").PreviewKind);
        Assert.Equal(FilePreviewKind.Image, items.Single(item => item.Name == "photo.JPG").PreviewKind);
        Assert.Equal(FilePreviewKind.Video, items.Single(item => item.Name == "video.MP4").PreviewKind);
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
    public async Task SearchDirectoryAsync_AssignsRelativeDirectoryPathFromSearchDirectory()
    {
        var childDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Child"));
        Directory.CreateDirectory(Path.Combine(childDirectory.FullName, "nested-match-folder"));
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "root-match.txt"), "root");
        await File.WriteAllTextAsync(Path.Combine(childDirectory.FullName, "nested-match.txt"), "nested");

        var items = await _service.SearchDirectoryAsync(_temporaryDirectory, "match");

        Assert.Equal(
            ".",
            items.Single(item => item.Name == "root-match.txt").RelativePath);
        Assert.Equal(
            "Child",
            items.Single(item => item.Name == "nested-match-folder").RelativePath);
        Assert.Equal(
            "Child",
            items.Single(item => item.Name == "nested-match.txt").RelativePath);
    }

    [Fact]
    public async Task SearchDirectoryAsync_AppliesSortOptions()
    {
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "alpha-match.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "zeta-match.txt"), "z");

        var items = await _service.SearchDirectoryAsync(
            _temporaryDirectory,
            "match",
            new FileSortOptions(FileSortField.Name, FileSortDirection.Descending));

        Assert.Equal(
            ["zeta-match.txt", "alpha-match.txt"],
            items.Select(item => item.Name));
    }

    [Fact]
    public async Task CreateDirectoryAsync_CreatesFolderWithPreferredName()
    {
        var createdPath = await _service.CreateDirectoryAsync(_temporaryDirectory, "New folder");

        Assert.Equal(Path.Combine(_temporaryDirectory, "New folder"), createdPath);
        Assert.True(Directory.Exists(createdPath));
    }

    [Fact]
    public async Task CreateDirectoryAsync_ExistingNameUsesExplorerStyleName()
    {
        Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "New folder"));
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "New folder (2)"), "file");

        var createdPath = await _service.CreateDirectoryAsync(_temporaryDirectory, "New folder");

        Assert.Equal(Path.Combine(_temporaryDirectory, "New folder (3)"), createdPath);
        Assert.True(Directory.Exists(createdPath));
    }

    [Fact]
    public async Task CreateDirectoryAsync_InvalidName_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateDirectoryAsync(
                _temporaryDirectory,
                Path.Combine("other", "New folder")));
    }

    [Fact]
    public async Task CreateDirectoryWithResultAsync_UndoRedoRemovesAndRecreatesFolder()
    {
        var result = await _service.CreateDirectoryWithResultAsync(
            _temporaryDirectory,
            "New folder");
        var createdPath = Assert.Single(result.Paths);

        Assert.Equal(FileOperationKind.Create, result.Operation);
        Assert.Equal(FileOperationEntryKind.Created, Assert.Single(result.Entries).Kind);
        Assert.True(Directory.Exists(createdPath));

        await _service.UndoFileOperationAsync(result);

        Assert.False(Directory.Exists(createdPath));

        await _service.RedoFileOperationAsync(result);

        Assert.True(Directory.Exists(createdPath));
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
    public async Task RenameWithResultAsync_UndoRedoRenamesFile()
    {
        var filePath = Path.Combine(_temporaryDirectory, "old.txt");
        await File.WriteAllTextAsync(filePath, "content");

        var result = await _service.RenameWithResultAsync(filePath, "new.txt");
        var renamedPath = Assert.Single(result.Paths);

        Assert.Equal(FileOperationKind.Rename, result.Operation);
        Assert.Equal(FileOperationEntryKind.Renamed, Assert.Single(result.Entries).Kind);
        Assert.Equal(Path.Combine(_temporaryDirectory, "new.txt"), renamedPath);
        Assert.False(File.Exists(filePath));
        Assert.Equal("content", await File.ReadAllTextAsync(renamedPath));

        await _service.UndoFileOperationAsync(result);

        Assert.True(File.Exists(filePath));
        Assert.False(File.Exists(renamedPath));
        Assert.Equal("content", await File.ReadAllTextAsync(filePath));

        await _service.RedoFileOperationAsync(result);

        Assert.False(File.Exists(filePath));
        Assert.True(File.Exists(renamedPath));
        Assert.Equal("content", await File.ReadAllTextAsync(renamedPath));
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
    public async Task DeleteWithResultAsync_UndoRedoRestoresDeletedFileAndDirectory()
    {
        var filePath = Path.Combine(_temporaryDirectory, "delete.txt");
        var directoryPath = Path.Combine(_temporaryDirectory, "Delete Folder");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllTextAsync(filePath, "delete");
        await File.WriteAllTextAsync(Path.Combine(directoryPath, "child.txt"), "child");

        var result = await _service.DeleteWithResultAsync(
            [filePath, directoryPath],
            FileDeleteBehavior.Permanent);

        Assert.Equal(FileOperationKind.Delete, result.Operation);
        Assert.Equal(
            [FileOperationEntryKind.Deleted, FileOperationEntryKind.Deleted],
            result.Entries.Select(entry => entry.Kind));
        Assert.False(File.Exists(filePath));
        Assert.False(Directory.Exists(directoryPath));

        await _service.UndoFileOperationAsync(result);

        Assert.Equal("delete", await File.ReadAllTextAsync(filePath));
        Assert.Equal("child", await File.ReadAllTextAsync(Path.Combine(directoryPath, "child.txt")));

        await _service.RedoFileOperationAsync(result);

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
    public async Task CopyAsync_DestinationNameExistsUsesExplorerStyleCopyName()
    {
        var sourcePath = Path.Combine(_temporaryDirectory, "source.txt");
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Destination"));
        await File.WriteAllTextAsync(sourcePath, "copy");
        await File.WriteAllTextAsync(Path.Combine(destinationDirectory.FullName, "source.txt"), "existing");

        var copiedPaths = await _service.CopyAsync([sourcePath], destinationDirectory.FullName);

        var copiedPath = Assert.Single(copiedPaths);
        Assert.Equal(Path.Combine(destinationDirectory.FullName, "source - Copy.txt"), copiedPath);
        Assert.Equal("copy", await File.ReadAllTextAsync(copiedPath));
        Assert.Equal("existing", await File.ReadAllTextAsync(Path.Combine(destinationDirectory.FullName, "source.txt")));
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
    public async Task CopyAsync_WithProgressReportsBytesAndCompletion()
    {
        var sourcePath = Path.Combine(_temporaryDirectory, "source.txt");
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Destination"));
        await File.WriteAllTextAsync(sourcePath, "copy");
        var progress = new RecordingProgress<FileOperationProgress>();

        await _service.CopyAsync([sourcePath], destinationDirectory.FullName, progress);

        Assert.Contains(
            progress.Values,
            value => value.Stage == FileOperationProgressStage.Preparing &&
                value.Operation == FileOperationKind.Copy);
        Assert.Contains(
            progress.Values,
            value => value.Stage == FileOperationProgressStage.Processing &&
                value.CurrentItemName == "source.txt" &&
                value.TotalBytes == 4);

        var completed = progress.Values.Last(value => value.Stage == FileOperationProgressStage.Completed);
        Assert.Equal(1, completed.CompletedItems);
        Assert.Equal(1, completed.TotalItems);
        Assert.Equal(4, completed.CompletedBytes);
        Assert.Equal(4, completed.TotalBytes);
        Assert.Equal(100, completed.PercentComplete);
    }

    [Fact]
    public async Task CopyAsync_CancellationFromProgressStopsBeforeCurrentFileIsCopied()
    {
        var sourcePath = Path.Combine(_temporaryDirectory, "source.txt");
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Destination"));
        await File.WriteAllTextAsync(sourcePath, "copy");
        using var cancellation = new CancellationTokenSource();
        var progress = new DelegatingProgress<FileOperationProgress>(value =>
        {
            if (value.Stage == FileOperationProgressStage.Processing &&
                value.CurrentItemName == "source.txt")
            {
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.CopyAsync(
                [sourcePath],
                destinationDirectory.FullName,
                progress,
                cancellation.Token));
        Assert.False(File.Exists(Path.Combine(destinationDirectory.FullName, "source.txt")));
    }

    [Fact]
    public async Task CopyAsync_CancellationDuringFileCopyRemovesPartialFile()
    {
        var sourcePath = Path.Combine(_temporaryDirectory, "large-source.bin");
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Destination"));
        await File.WriteAllBytesAsync(sourcePath, new byte[2 * 1024 * 1024]);
        using var cancellation = new CancellationTokenSource();
        var progress = new DelegatingProgress<FileOperationProgress>(value =>
        {
            if (value.CompletedBytes > 0)
            {
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.CopyAsync(
                [sourcePath],
                destinationDirectory.FullName,
                progress,
                cancellation.Token));
        Assert.False(File.Exists(Path.Combine(destinationDirectory.FullName, "large-source.bin")));
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
    public async Task MoveAsync_SameDirectoryReturnsOriginalPath()
    {
        var sourcePath = Path.Combine(_temporaryDirectory, "source.txt");
        await File.WriteAllTextAsync(sourcePath, "move");

        var movedPaths = await _service.MoveAsync([sourcePath], _temporaryDirectory);

        var movedPath = Assert.Single(movedPaths);
        Assert.Equal(sourcePath, movedPath);
        Assert.True(File.Exists(sourcePath));
        Assert.Equal("move", await File.ReadAllTextAsync(sourcePath));
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

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value)
        {
            Values.Add(value);
        }
    }

    private sealed class DelegatingProgress<T> : IProgress<T>
    {
        private readonly Action<T> _onReport;

        public DelegatingProgress(Action<T> onReport)
        {
            _onReport = onReport;
        }

        public void Report(T value)
        {
            _onReport(value);
        }
    }
}
