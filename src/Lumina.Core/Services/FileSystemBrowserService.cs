using Lumina.Core.Models;
using Microsoft.VisualBasic.FileIO;

namespace Lumina.Core.Services;

public sealed class FileSystemBrowserService : IFileBrowserService
{
    private const int FileCopyBufferSize = 1024 * 1024;

    private static readonly HashSet<string> ImagePreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avif",
        ".bmp",
        ".dib",
        ".gif",
        ".heic",
        ".heif",
        ".ico",
        ".jfif",
        ".jpe",
        ".jpeg",
        ".jpg",
        ".png",
        ".svg",
        ".tif",
        ".tiff",
        ".webp",
    };

    private static readonly HashSet<string> VideoPreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3g2",
        ".3gp",
        ".avi",
        ".m2ts",
        ".m4v",
        ".mkv",
        ".mov",
        ".mp4",
        ".mpeg",
        ".mpg",
        ".mts",
        ".webm",
        ".wmv",
    };

    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
    };

    private static readonly EnumerationOptions RecursiveEnumerationOptions = new()
    {
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
    };

    private readonly ITagParserService _tagParserService;

    public FileSystemBrowserService()
        : this(new TagParserService())
    {
    }

    public FileSystemBrowserService(ITagParserService tagParserService)
    {
        _tagParserService = tagParserService;
    }

    public Task<IReadOnlyList<FileItem>> LoadDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        return LoadDirectoryAsync(
            directoryPath,
            FileSortOptions.Default,
            cancellationToken);
    }

    public Task<IReadOnlyList<FileItem>> LoadDirectoryAsync(
        string directoryPath,
        FileSortOptions sortOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(sortOptions);

        return Task.Run(
            () => LoadDirectory(directoryPath.Trim(), sortOptions, cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<FileItem>> SearchDirectoryAsync(
        string directoryPath,
        string query,
        CancellationToken cancellationToken = default)
    {
        return SearchDirectoryAsync(
            directoryPath,
            query,
            FileSortOptions.Default,
            cancellationToken);
    }

    public Task<IReadOnlyList<FileItem>> SearchDirectoryAsync(
        string directoryPath,
        string query,
        FileSortOptions sortOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sortOptions);

        return Task.Run(
            () => SearchDirectory(directoryPath.Trim(), query.Trim(), sortOptions, cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<FileItem>> FilterDirectoryByTagsAsync(
        string directoryPath,
        IReadOnlyList<string> requiredTags,
        CancellationToken cancellationToken = default)
    {
        return FilterDirectoryByTagsAsync(
            directoryPath,
            requiredTags,
            FileSortOptions.Default,
            cancellationToken);
    }

    public Task<IReadOnlyList<FileItem>> FilterDirectoryByTagsAsync(
        string directoryPath,
        IReadOnlyList<string> requiredTags,
        FileSortOptions sortOptions,
        CancellationToken cancellationToken = default)
    {
        return FilterDirectoryByTagsAsync(
            directoryPath,
            requiredTags,
            string.Empty,
            sortOptions,
            cancellationToken);
    }

    public Task<IReadOnlyList<FileItem>> FilterDirectoryByTagsAsync(
        string directoryPath,
        IReadOnlyList<string> requiredTags,
        string query,
        FileSortOptions sortOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(requiredTags);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sortOptions);

        return Task.Run(
            () => FilterDirectoryByTags(
                directoryPath.Trim(),
                requiredTags,
                query.Trim(),
                sortOptions,
                cancellationToken),
            cancellationToken);
    }

    public async Task<string> CreateDirectoryAsync(
        string parentDirectoryPath,
        string preferredName,
        CancellationToken cancellationToken = default)
    {
        var result = await CreateDirectoryWithResultAsync(
            parentDirectoryPath,
            preferredName,
            cancellationToken);

        return result.Paths.Single();
    }

    public Task<FileOperationResult> CreateDirectoryWithResultAsync(
        string parentDirectoryPath,
        string preferredName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredName);

        return Task.Run(
            () => CreateDirectoryWithResult(parentDirectoryPath.Trim(), preferredName.Trim(), cancellationToken),
            cancellationToken);
    }

    public async Task<string> RenameAsync(
        string path,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var result = await RenameWithResultAsync(
            path,
            newName,
            cancellationToken);

        return result.Paths.Single();
    }

    public Task<FileOperationResult> RenameWithResultAsync(
        string path,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        return Task.Run(
            () => RenameWithResult(path.Trim(), newName.Trim(), cancellationToken),
            cancellationToken);
    }

    public Task DeleteAsync(
        IReadOnlyList<string> paths,
        FileDeleteBehavior deleteBehavior,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return Task.Run(
            () => Delete(paths, deleteBehavior, cancellationToken),
            cancellationToken);
    }

    public Task<FileOperationResult> DeleteWithResultAsync(
        IReadOnlyList<string> paths,
        FileDeleteBehavior deleteBehavior,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return Task.Run(
            () => DeleteWithResult(paths, deleteBehavior, cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<string>> CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        return CopyAsync(
            sourcePaths,
            destinationDirectoryPath,
            progress: null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var result = await CopyWithResultAsync(
            sourcePaths,
            destinationDirectoryPath,
            progress,
            conflictResolver: null,
            cancellationToken);

        return result.Paths;
    }

    public Task<FileOperationResult> CopyWithResultAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress = null,
        IFileOperationConflictResolver? conflictResolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);

        return Task.Run(
            () => Copy(
                sourcePaths,
                destinationDirectoryPath.Trim(),
                progress,
                conflictResolver,
                cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<string>> MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        return MoveAsync(
            sourcePaths,
            destinationDirectoryPath,
            progress: null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var result = await MoveWithResultAsync(
            sourcePaths,
            destinationDirectoryPath,
            progress,
            conflictResolver: null,
            cancellationToken);

        return result.Paths;
    }

    public Task<FileOperationResult> MoveWithResultAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress = null,
        IFileOperationConflictResolver? conflictResolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);

        return Task.Run(
            () => Move(
                sourcePaths,
                destinationDirectoryPath.Trim(),
                progress,
                conflictResolver,
                cancellationToken),
            cancellationToken);
    }

    public Task UndoFileOperationAsync(
        FileOperationResult operationResult,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationResult);

        return Task.Run(
            () => UndoFileOperation(operationResult, progress, cancellationToken),
            cancellationToken);
    }

    public Task RedoFileOperationAsync(
        FileOperationResult operationResult,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationResult);

        return Task.Run(
            () => RedoFileOperation(operationResult, progress, cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<FileItem> LoadDirectory(
        string directoryPath,
        FileSortOptions sortOptions,
        CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        return SortFileItems(
            directory
                .EnumerateFileSystemInfos("*", EnumerationOptions)
                .Select(info =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CreateFileItem(info);
                }),
            sortOptions,
            includePathTieBreaker: false);
    }

    private IReadOnlyList<FileItem> SearchDirectory(
        string directoryPath,
        string query,
        FileSortOptions sortOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return LoadDirectory(directoryPath, sortOptions, cancellationToken);
        }

        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        return SortFileItems(
            directory
                .EnumerateFileSystemInfos("*", RecursiveEnumerationOptions)
                .Select(info =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CreateFileItem(info, directory.FullName);
                })
                .Where(item => MatchesQuery(item, query)),
            sortOptions,
            includePathTieBreaker: true);
    }

    private IReadOnlyList<FileItem> FilterDirectoryByTags(
        string directoryPath,
        IReadOnlyList<string> requiredTags,
        string query,
        FileSortOptions sortOptions,
        CancellationToken cancellationToken)
    {
        var normalizedRequiredTags = NormalizeRequiredTags(requiredTags);
        if (normalizedRequiredTags.Count == 0)
        {
            return string.IsNullOrWhiteSpace(query)
                ? LoadDirectory(directoryPath, sortOptions, cancellationToken)
                : SearchDirectory(directoryPath, query, sortOptions, cancellationToken);
        }

        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        return SortFileItems(
            directory
                .EnumerateFileSystemInfos("*", RecursiveEnumerationOptions)
                .Select(info =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CreateFileItem(info, directory.FullName);
                })
                .Where(item =>
                    !item.IsDirectory &&
                    MatchesRequiredTags(item, normalizedRequiredTags) &&
                    (string.IsNullOrWhiteSpace(query) || MatchesQuery(item, query))),
            sortOptions,
            includePathTieBreaker: true);
    }

    private string CreateDirectory(
        string parentDirectoryPath,
        string preferredName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ValidateFileName(preferredName);

        var parentDirectory = GetExistingDirectory(parentDirectoryPath);
        var directoryPath = ResolveDirectoryCreationPath(
            parentDirectory.FullName,
            preferredName);
        Directory.CreateDirectory(directoryPath);

        return directoryPath;
    }

    private FileOperationResult CreateDirectoryWithResult(
        string parentDirectoryPath,
        string preferredName,
        CancellationToken cancellationToken)
    {
        var directoryPath = CreateDirectory(
            parentDirectoryPath,
            preferredName,
            cancellationToken);

        return new FileOperationResult
        {
            Operation = FileOperationKind.Create,
            Paths = [directoryPath],
            Entries =
            [
                new FileOperationEntry
                {
                    Kind = FileOperationEntryKind.Created,
                    DestinationPath = directoryPath,
                    IsDirectory = true,
                },
            ],
        };
    }

    private string Rename(
        string path,
        string newName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ValidateFileName(newName);

        var sourcePath = NormalizePath(path);
        if (Directory.Exists(sourcePath))
        {
            var parentPath = Directory.GetParent(sourcePath)?.FullName
                ?? throw new IOException($"Cannot rename root directory: {sourcePath}");
            var destinationPath = Path.Combine(parentPath, newName);
            MoveDirectory(sourcePath, destinationPath);

            return destinationPath;
        }

        if (File.Exists(sourcePath))
        {
            var parentPath = Path.GetDirectoryName(sourcePath)
                ?? throw new IOException($"Cannot resolve parent directory: {sourcePath}");
            var destinationPath = Path.Combine(parentPath, newName);
            MoveFile(sourcePath, destinationPath);

            return destinationPath;
        }

        throw new FileNotFoundException($"File or directory not found: {sourcePath}", sourcePath);
    }

    private FileOperationResult RenameWithResult(
        string path,
        string newName,
        CancellationToken cancellationToken)
    {
        var sourcePath = NormalizePath(path);
        var isDirectory = Directory.Exists(sourcePath);
        var destinationPath = Rename(path, newName, cancellationToken);

        IReadOnlyList<FileOperationEntry> entries = IsSamePath(sourcePath, destinationPath)
            ? []
            :
            [
                new FileOperationEntry
                {
                    Kind = FileOperationEntryKind.Renamed,
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath,
                    IsDirectory = isDirectory,
                },
            ];

        return new FileOperationResult
        {
            Operation = FileOperationKind.Rename,
            Paths = [destinationPath],
            Entries = entries,
        };
    }

    private void Delete(
        IReadOnlyList<string> paths,
        FileDeleteBehavior deleteBehavior,
        CancellationToken cancellationToken)
    {
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeletePath(path, deleteBehavior);
        }
    }

    private FileOperationResult DeleteWithResult(
        IReadOnlyList<string> paths,
        FileDeleteBehavior deleteBehavior,
        CancellationToken cancellationToken)
    {
        var deletedPaths = new List<string>(paths.Count);
        var entries = new List<FileOperationEntry>(paths.Count);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = GetExistingPath(path);
            var isDirectory = Directory.Exists(normalizedPath);
            var backupPath = BackupPath(normalizedPath, cancellationToken);

            DeletePath(normalizedPath, deleteBehavior);
            deletedPaths.Add(normalizedPath);
            entries.Add(new FileOperationEntry
            {
                Kind = FileOperationEntryKind.Deleted,
                SourcePath = normalizedPath,
                BackupPath = backupPath,
                IsDirectory = isDirectory,
            });
        }

        return new FileOperationResult
        {
            Operation = FileOperationKind.Delete,
            Paths = deletedPaths,
            Entries = entries,
        };
    }

    private static void UndoFileOperation(
        FileOperationResult operationResult,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var progressState = CreateHistoryProgressState(operationResult, progress);
        foreach (var entry in operationResult.Entries.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            progressState.ReportItemStarted(entry.DestinationPath, entry.SourcePath);

            switch (entry.Kind)
            {
                case FileOperationEntryKind.Created:
                    DeleteExistingPath(entry.DestinationPath);
                    break;
                case FileOperationEntryKind.Renamed:
                    MovePathSimple(entry.DestinationPath, entry.SourcePath);
                    break;
                case FileOperationEntryKind.Deleted:
                    RestoreBackupPath(entry.BackupPath, entry.SourcePath, cancellationToken);
                    break;
                case FileOperationEntryKind.Copied:
                    DeleteExistingPath(entry.DestinationPath);
                    break;
                case FileOperationEntryKind.Moved:
                    MovePathSimple(entry.DestinationPath, entry.SourcePath);
                    break;
                case FileOperationEntryKind.ReplacedByCopy:
                    DeleteExistingPath(entry.DestinationPath);
                    CopyPathForBackup(entry.BackupPath, entry.DestinationPath, cancellationToken);
                    break;
                case FileOperationEntryKind.ReplacedByMove:
                    MovePathSimple(entry.DestinationPath, entry.SourcePath);
                    CopyPathForBackup(entry.BackupPath, entry.DestinationPath, cancellationToken);
                    break;
            }

            progressState.ReportItemCompleted(
                entry.DestinationPath,
                entry.SourcePath,
                completedBytes: 0);
        }

        progressState.ReportCompleted();
    }

    private static void RedoFileOperation(
        FileOperationResult operationResult,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var progressState = CreateHistoryProgressState(operationResult, progress);
        foreach (var entry in operationResult.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progressState.ReportItemStarted(entry.SourcePath, entry.DestinationPath);

            switch (entry.Kind)
            {
                case FileOperationEntryKind.Created:
                    RecreateCreatedPath(entry);
                    break;
                case FileOperationEntryKind.Renamed:
                    MovePathSimple(entry.SourcePath, entry.DestinationPath);
                    break;
                case FileOperationEntryKind.Deleted:
                    DeleteExistingPath(entry.SourcePath);
                    break;
                case FileOperationEntryKind.Copied:
                    CopyPathForBackup(entry.SourcePath, entry.DestinationPath, cancellationToken);
                    break;
                case FileOperationEntryKind.Moved:
                    MovePathSimple(entry.SourcePath, entry.DestinationPath);
                    break;
                case FileOperationEntryKind.ReplacedByCopy:
                    DeleteExistingPath(entry.DestinationPath);
                    CopyPathForBackup(entry.SourcePath, entry.DestinationPath, cancellationToken);
                    break;
                case FileOperationEntryKind.ReplacedByMove:
                    DeleteExistingPath(entry.DestinationPath);
                    MovePathSimple(entry.SourcePath, entry.DestinationPath);
                    break;
            }

            progressState.ReportItemCompleted(
                entry.SourcePath,
                entry.DestinationPath,
                completedBytes: 0);
        }

        progressState.ReportCompleted();
    }

    private FileOperationResult Copy(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress,
        IFileOperationConflictResolver? conflictResolver,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = GetExistingDirectory(destinationDirectoryPath);
        var copiedPaths = new List<string>(sourcePaths.Count);
        var entries = new List<FileOperationEntry>();
        var progressState = CreateProgressState(
            FileOperationKind.Copy,
            sourcePaths,
            progress,
            cancellationToken);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedSourcePath = GetExistingPath(sourcePath);
            EnsureCanCopyIntoDirectory(normalizedSourcePath, destinationDirectory.FullName);

            var destinationPath = ResolveTransferDestinationPath(
                FileOperationKind.Copy,
                normalizedSourcePath,
                destinationDirectory.FullName,
                conflictResolver,
                cancellationToken,
                out var action);
            if (action == FileConflictAction.Skip)
            {
                continue;
            }

            CopyPath(
                normalizedSourcePath,
                destinationPath,
                progressState,
                entries,
                action,
                cancellationToken);
            if (!IsSamePath(normalizedSourcePath, destinationPath))
            {
                copiedPaths.Add(destinationPath);
            }
        }

        progressState.ReportCompleted();

        return new FileOperationResult
        {
            Operation = FileOperationKind.Copy,
            Paths = copiedPaths,
            Entries = entries,
        };
    }

    private FileOperationResult Move(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress,
        IFileOperationConflictResolver? conflictResolver,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = GetExistingDirectory(destinationDirectoryPath);
        var movedPaths = new List<string>(sourcePaths.Count);
        var entries = new List<FileOperationEntry>();
        var progressState = CreateProgressState(
            FileOperationKind.Move,
            sourcePaths,
            progress,
            cancellationToken);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedSourcePath = GetExistingPath(sourcePath);
            var preferredDestinationPath = Path.Combine(
                destinationDirectory.FullName,
                GetPathName(normalizedSourcePath));

            if (IsSamePath(normalizedSourcePath, preferredDestinationPath))
            {
                movedPaths.Add(normalizedSourcePath);
                progressState.ReportItemCompleted(
                    normalizedSourcePath,
                    normalizedSourcePath,
                    completedBytes: 0);
                continue;
            }

            EnsureCanMoveIntoDirectory(normalizedSourcePath, destinationDirectory.FullName);

            var destinationPath = ResolveTransferDestinationPath(
                FileOperationKind.Move,
                normalizedSourcePath,
                destinationDirectory.FullName,
                conflictResolver,
                cancellationToken,
                out var action);
            if (action == FileConflictAction.Skip)
            {
                continue;
            }

            MovePath(
                normalizedSourcePath,
                destinationPath,
                progressState,
                entries,
                action,
                cancellationToken);
            movedPaths.Add(destinationPath);
        }

        progressState.ReportCompleted();

        return new FileOperationResult
        {
            Operation = FileOperationKind.Move,
            Paths = movedPaths,
            Entries = entries,
        };
    }

    private FileItem CreateFileItem(
        FileSystemInfo info,
        string? relativeRootPath = null)
    {
        var isDirectory = info.Attributes.HasFlag(FileAttributes.Directory);
        var name = info.Name;
        var displayName = _tagParserService.GetDisplayName(name);

        return new FileItem
        {
            Name = name,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName,
            Path = info.FullName,
            RelativePath = ResolveRelativeDirectoryPath(relativeRootPath, info.FullName),
            IsDirectory = isDirectory,
            PreviewKind = isDirectory ? FilePreviewKind.None : ResolvePreviewKind(name),
            Size = isDirectory ? 0 : ((FileInfo)info).Length,
            Modified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            Created = new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero),
            Tags = _tagParserService.ParseTagsFromFilename(name),
        };
    }

    private static string ResolveRelativeDirectoryPath(
        string? relativeRootPath,
        string path)
    {
        if (string.IsNullOrWhiteSpace(relativeRootPath))
        {
            return string.Empty;
        }

        var directoryPath = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return string.Empty;
        }

        return Path.GetRelativePath(relativeRootPath, directoryPath);
    }

    private static FilePreviewKind ResolvePreviewKind(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (ImagePreviewExtensions.Contains(extension))
        {
            return FilePreviewKind.Image;
        }

        return VideoPreviewExtensions.Contains(extension)
            ? FilePreviewKind.Video
            : FilePreviewKind.None;
    }

    private static bool MatchesQuery(FileItem item, string query)
    {
        return item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.Tags.Any(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private static bool MatchesRequiredTags(
        FileItem item,
        IReadOnlySet<string> requiredTags)
    {
        var fileTags = item.Tags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requiredTags.All(fileTags.Contains);
    }

    private static IReadOnlySet<string> NormalizeRequiredTags(
        IReadOnlyList<string> requiredTags)
    {
        return requiredTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FileItem> SortFileItems(
        IEnumerable<FileItem> items,
        FileSortOptions sortOptions,
        bool includePathTieBreaker)
    {
        var ordered = items.OrderByDescending(item => item.IsDirectory);
        ordered = sortOptions.Field switch
        {
            FileSortField.Name => ThenByString(
                ThenByString(
                    ordered,
                    sortOptions.Direction,
                    item => item.DisplayName),
                sortOptions.Direction,
                item => item.Name),
            FileSortField.Modified => ThenByDate(
                ordered,
                sortOptions.Direction,
                item => item.Modified),
            FileSortField.Type => ThenByString(
                ordered,
                sortOptions.Direction,
                GetFileTypeSortKey),
            FileSortField.Size => ThenByLong(
                ordered,
                sortOptions.Direction,
                item => item.Size),
            FileSortField.Created => ThenByDate(
                ordered,
                sortOptions.Direction,
                item => item.Created),
            _ => ThenByString(
                ordered,
                sortOptions.Direction,
                item => item.DisplayName),
        };

        if (sortOptions.Field != FileSortField.Name)
        {
            ordered = ordered
                .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase);
        }

        if (includePathTieBreaker)
        {
            ordered = ordered.ThenBy(item => item.Path, StringComparer.CurrentCultureIgnoreCase);
        }

        return ordered.ToList();
    }

    private static IOrderedEnumerable<FileItem> ThenByString(
        IOrderedEnumerable<FileItem> items,
        FileSortDirection direction,
        Func<FileItem, string> keySelector)
    {
        return direction == FileSortDirection.Descending
            ? items.ThenByDescending(keySelector, StringComparer.CurrentCultureIgnoreCase)
            : items.ThenBy(keySelector, StringComparer.CurrentCultureIgnoreCase);
    }

    private static IOrderedEnumerable<FileItem> ThenByLong(
        IOrderedEnumerable<FileItem> items,
        FileSortDirection direction,
        Func<FileItem, long> keySelector)
    {
        return direction == FileSortDirection.Descending
            ? items.ThenByDescending(keySelector)
            : items.ThenBy(keySelector);
    }

    private static IOrderedEnumerable<FileItem> ThenByDate(
        IOrderedEnumerable<FileItem> items,
        FileSortDirection direction,
        Func<FileItem, DateTimeOffset> keySelector)
    {
        return direction == FileSortDirection.Descending
            ? items.ThenByDescending(keySelector)
            : items.ThenBy(keySelector);
    }

    private static string GetFileTypeSortKey(FileItem item)
    {
        if (item.IsDirectory)
        {
            return "folder";
        }

        var extension = Path.GetExtension(item.Name).TrimStart('.');

        return string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension;
    }

    private static void MoveDirectory(string sourcePath, string destinationPath)
    {
        if (IsSamePath(sourcePath, destinationPath))
        {
            return;
        }

        if (PathExists(destinationPath))
        {
            throw new IOException($"Destination already exists: {destinationPath}");
        }

        Directory.Move(sourcePath, destinationPath);
    }

    private static void MoveFile(string sourcePath, string destinationPath)
    {
        if (IsSamePath(sourcePath, destinationPath))
        {
            return;
        }

        if (PathExists(destinationPath))
        {
            throw new IOException($"Destination already exists: {destinationPath}");
        }

        File.Move(sourcePath, destinationPath);
    }

    private static void DeletePath(string path, FileDeleteBehavior deleteBehavior)
    {
        var normalizedPath = GetExistingPath(path);
        if (deleteBehavior == FileDeleteBehavior.RecycleBin)
        {
            DeletePathToRecycleBin(normalizedPath);
            return;
        }

        if (Directory.Exists(normalizedPath))
        {
            Directory.Delete(normalizedPath, recursive: true);
            return;
        }

        File.Delete(normalizedPath);
    }

    private static void DeletePathToRecycleBin(string path)
    {
        if (Directory.Exists(path))
        {
            FileSystem.DeleteDirectory(
                path,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin);
            return;
        }

        FileSystem.DeleteFile(
            path,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin);
    }

    private static void CopyPath(
        string sourcePath,
        string destinationPath,
        FileOperationProgressState progressState,
        List<FileOperationEntry> entries,
        FileConflictAction action,
        CancellationToken cancellationToken)
    {
        string backupPath = string.Empty;
        if (PathExists(destinationPath) && action == FileConflictAction.Replace)
        {
            backupPath = BackupPath(destinationPath, cancellationToken);
            DeleteExistingPath(destinationPath);
        }

        if (Directory.Exists(sourcePath))
        {
            progressState.ReportItemStarted(sourcePath, destinationPath);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(destinationPath);

            try
            {
                foreach (var childPath in Directory.EnumerateFileSystemEntries(
                             sourcePath,
                             "*",
                             EnumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var childDestinationPath = Path.Combine(
                        destinationPath,
                        Path.GetFileName(childPath));
                    var childAction = FileConflictAction.KeepBoth;
                    if (PathExists(childDestinationPath))
                    {
                        childDestinationPath = ResolveCopyDestinationPath(
                            childPath,
                            destinationPath);
                    }

                    CopyPath(
                        childPath,
                        childDestinationPath,
                        progressState,
                        entries,
                        childAction,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                TryDeleteDirectory(destinationPath);
                throw;
            }

            CopyDirectoryMetadata(sourcePath, destinationPath);
            progressState.ReportItemCompleted(
                sourcePath,
                destinationPath,
                completedBytes: 0);
            if (action != FileConflictAction.Merge)
            {
                entries.Add(new FileOperationEntry
                {
                    Kind = string.IsNullOrWhiteSpace(backupPath)
                        ? FileOperationEntryKind.Copied
                        : FileOperationEntryKind.ReplacedByCopy,
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath,
                    BackupPath = backupPath,
                    IsDirectory = true,
                });
            }

            return;
        }

        CopyFile(sourcePath, destinationPath, progressState, cancellationToken);
        entries.Add(new FileOperationEntry
        {
            Kind = string.IsNullOrWhiteSpace(backupPath)
                ? FileOperationEntryKind.Copied
                : FileOperationEntryKind.ReplacedByCopy,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            BackupPath = backupPath,
            IsDirectory = false,
        });
    }

    private static void MovePath(
        string sourcePath,
        string destinationPath,
        FileOperationProgressState progressState,
        List<FileOperationEntry> entries,
        FileConflictAction action,
        CancellationToken cancellationToken)
    {
        if (action == FileConflictAction.Merge &&
            Directory.Exists(sourcePath) &&
            Directory.Exists(destinationPath))
        {
            foreach (var childPath in Directory.EnumerateFileSystemEntries(
                         sourcePath,
                         "*",
                         EnumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var childDestinationPath = Path.Combine(
                    destinationPath,
                    Path.GetFileName(childPath));
                if (PathExists(childDestinationPath))
                {
                    childDestinationPath = ResolveCopyDestinationPath(
                        childPath,
                        destinationPath);
                }

                MovePath(
                    childPath,
                    childDestinationPath,
                    progressState,
                    entries,
                    FileConflictAction.KeepBoth,
                    cancellationToken);
            }

            TryDeleteDirectory(sourcePath);
            return;
        }

        string backupPath = string.Empty;
        if (PathExists(destinationPath) && action == FileConflictAction.Replace)
        {
            backupPath = BackupPath(destinationPath, cancellationToken);
            DeleteExistingPath(destinationPath);
        }

        progressState.ReportItemStarted(sourcePath, destinationPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
        }
        else
        {
            File.Move(sourcePath, destinationPath);
        }

        progressState.ReportItemCompleted(
            sourcePath,
            destinationPath,
            completedBytes: 0);
        entries.Add(new FileOperationEntry
        {
            Kind = string.IsNullOrWhiteSpace(backupPath)
                ? FileOperationEntryKind.Moved
                : FileOperationEntryKind.ReplacedByMove,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            BackupPath = backupPath,
            IsDirectory = Directory.Exists(destinationPath),
        });
    }

    private static void CopyFile(
        string sourcePath,
        string destinationPath,
        FileOperationProgressState progressState,
        CancellationToken cancellationToken)
    {
        progressState.ReportItemStarted(sourcePath, destinationPath);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceInfo = new FileInfo(sourcePath);
        try
        {
            using (var source = new FileStream(
                       sourcePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       FileCopyBufferSize,
                       FileOptions.SequentialScan))
            using (var destination = new FileStream(
                       destinationPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       FileCopyBufferSize,
                       FileOptions.SequentialScan))
            {
                var buffer = new byte[FileCopyBufferSize];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bytesRead = source.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    destination.Write(buffer, 0, bytesRead);
                    progressState.ReportBytesCompleted(
                        sourcePath,
                        destinationPath,
                        bytesRead);
                }
            }
        }
        catch (OperationCanceledException)
        {
            TryDeleteFile(destinationPath);
            throw;
        }

        CopyFileMetadata(sourceInfo, destinationPath);
        progressState.ReportItemCompleted(
            sourcePath,
            destinationPath,
            completedBytes: 0);
    }

    private static void CopyDirectoryMetadata(string sourcePath, string destinationPath)
    {
        var sourceInfo = new DirectoryInfo(sourcePath);
        var destinationInfo = new DirectoryInfo(destinationPath);

        destinationInfo.CreationTimeUtc = sourceInfo.CreationTimeUtc;
        destinationInfo.LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc;
        destinationInfo.Attributes = sourceInfo.Attributes;
    }

    private static void CopyFileMetadata(FileInfo sourceInfo, string destinationPath)
    {
        var destinationInfo = new FileInfo(destinationPath);

        destinationInfo.CreationTimeUtc = sourceInfo.CreationTimeUtc;
        destinationInfo.LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc;
        destinationInfo.Attributes = sourceInfo.Attributes;
    }

    private static string ResolveTransferDestinationPath(
        FileOperationKind operation,
        string sourcePath,
        string destinationDirectoryPath,
        IFileOperationConflictResolver? conflictResolver,
        CancellationToken cancellationToken,
        out FileConflictAction action)
    {
        var destinationPath = Path.Combine(destinationDirectoryPath, GetPathName(sourcePath));
        action = FileConflictAction.KeepBoth;

        if (!PathExists(destinationPath))
        {
            return destinationPath;
        }

        if (operation == FileOperationKind.Move &&
            IsSamePath(sourcePath, destinationPath))
        {
            action = FileConflictAction.Skip;
            return destinationPath;
        }

        action = ResolveConflictAction(
            operation,
            sourcePath,
            destinationPath,
            conflictResolver,
            cancellationToken);
        return action switch
        {
            FileConflictAction.Skip => destinationPath,
            FileConflictAction.Replace => destinationPath,
            FileConflictAction.Merge when Directory.Exists(sourcePath) && Directory.Exists(destinationPath) =>
                destinationPath,
            _ => ResolveCopyDestinationPath(sourcePath, destinationDirectoryPath),
        };
    }

    private static FileConflictAction ResolveConflictAction(
        FileOperationKind operation,
        string sourcePath,
        string destinationPath,
        IFileOperationConflictResolver? conflictResolver,
        CancellationToken cancellationToken)
    {
        if (conflictResolver is null)
        {
            return operation == FileOperationKind.Move
                ? throw new IOException($"Destination already exists: {destinationPath}")
                : FileConflictAction.KeepBoth;
        }

        var action = conflictResolver
            .ResolveAsync(
                new FileConflictInfo
                {
                    Operation = operation,
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath,
                    SourceIsDirectory = Directory.Exists(sourcePath),
                    DestinationIsDirectory = Directory.Exists(destinationPath),
                },
                cancellationToken)
            .GetAwaiter()
            .GetResult();

        if (action == FileConflictAction.Merge &&
            (!Directory.Exists(sourcePath) || !Directory.Exists(destinationPath)))
        {
            return FileConflictAction.Replace;
        }

        return action;
    }

    private static string BackupPath(
        string path,
        CancellationToken cancellationToken)
    {
        var backupRoot = Path.Combine(
            Path.GetTempPath(),
            "Lumina-FileOperationBackups",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupRoot);

        var backupPath = Path.Combine(backupRoot, GetPathName(path));
        CopyPathForBackup(path, backupPath, cancellationToken);

        return backupPath;
    }

    private static void CopyPathForBackup(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureParentDirectory(destinationPath);
        if (Directory.Exists(sourcePath))
        {
            Directory.CreateDirectory(destinationPath);
            foreach (var childPath in Directory.EnumerateFileSystemEntries(
                         sourcePath,
                         "*",
                         EnumerationOptions))
            {
                CopyPathForBackup(
                    childPath,
                    Path.Combine(destinationPath, Path.GetFileName(childPath)),
                    cancellationToken);
            }

            CopyDirectoryMetadata(sourcePath, destinationPath);
            return;
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
        CopyFileMetadata(new FileInfo(sourcePath), destinationPath);
    }

    private static void DeleteExistingPath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void RestoreBackupPath(
        string backupPath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !PathExists(backupPath))
        {
            throw new FileNotFoundException($"Backup path not found: {backupPath}", backupPath);
        }

        if (PathExists(destinationPath))
        {
            throw new IOException($"Destination already exists: {destinationPath}");
        }

        CopyPathForBackup(backupPath, destinationPath, cancellationToken);
    }

    private static void RecreateCreatedPath(FileOperationEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.DestinationPath))
        {
            return;
        }

        if (PathExists(entry.DestinationPath))
        {
            throw new IOException($"Destination already exists: {entry.DestinationPath}");
        }

        EnsureParentDirectory(entry.DestinationPath);
        if (entry.IsDirectory)
        {
            Directory.CreateDirectory(entry.DestinationPath);
            return;
        }

        using var _ = File.Create(entry.DestinationPath);
    }

    private static void MovePathSimple(
        string sourcePath,
        string destinationPath)
    {
        if (!PathExists(sourcePath))
        {
            throw new FileNotFoundException($"File or directory not found: {sourcePath}", sourcePath);
        }

        if (PathExists(destinationPath))
        {
            throw new IOException($"Destination already exists: {destinationPath}");
        }

        EnsureParentDirectory(destinationPath);
        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
            return;
        }

        File.Move(sourcePath, destinationPath);
    }

    private static void EnsureParentDirectory(string path)
    {
        var parentPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parentPath))
        {
            Directory.CreateDirectory(parentPath);
        }
    }

    private static FileOperationProgressState CreateHistoryProgressState(
        FileOperationResult operationResult,
        IProgress<FileOperationProgress>? progress)
    {
        var progressState = new FileOperationProgressState(operationResult.Operation, progress);
        progressState.SetTotals(new FileTransferTotals(operationResult.Entries.Count, 0));
        progressState.ReportProcessing();

        return progressState;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static FileOperationProgressState CreateProgressState(
        FileOperationKind operation,
        IReadOnlyList<string> sourcePaths,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var progressState = new FileOperationProgressState(operation, progress);
        if (progress is null)
        {
            return progressState;
        }

        progressState.ReportPreparing();

        if (operation == FileOperationKind.Copy)
        {
            var totals = CalculateTransferTotals(sourcePaths, cancellationToken);
            progressState.SetTotals(totals);
        }
        else
        {
            progressState.SetTotals(new FileTransferTotals(sourcePaths.Count, 0));
        }

        progressState.ReportProcessing();

        return progressState;
    }

    private static FileTransferTotals CalculateTransferTotals(
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken)
    {
        var totals = new FileTransferTotals(0, 0);
        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totals += CalculateTransferTotals(GetExistingPath(sourcePath), cancellationToken);
        }

        return totals;
    }

    private static FileTransferTotals CalculateTransferTotals(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(path))
        {
            var totals = new FileTransferTotals(1, 0);
            foreach (var childPath in Directory.EnumerateFileSystemEntries(
                         path,
                         "*",
                         EnumerationOptions))
            {
                totals += CalculateTransferTotals(childPath, cancellationToken);
            }

            return totals;
        }

        var fileInfo = new FileInfo(path);
        return new FileTransferTotals(1, fileInfo.Length);
    }

    private static string ResolveCopyDestinationPath(
        string sourcePath,
        string destinationDirectoryPath)
    {
        var originalDestinationPath = Path.Combine(destinationDirectoryPath, GetPathName(sourcePath));
        if (!PathExists(originalDestinationPath) && !IsSamePath(sourcePath, originalDestinationPath))
        {
            return originalDestinationPath;
        }

        var isSourceFile = File.Exists(sourcePath);
        var sourceName = GetPathName(sourcePath);
        var sourceNameWithoutExtension = isSourceFile
            ? Path.GetFileNameWithoutExtension(sourceName)
            : sourceName;
        var extension = isSourceFile ? Path.GetExtension(sourceName) : string.Empty;

        var firstCopyPath = Path.Combine(
            destinationDirectoryPath,
            $"{sourceNameWithoutExtension} - Copy{extension}");
        if (!PathExists(firstCopyPath))
        {
            return firstCopyPath;
        }

        for (var copyIndex = 2; copyIndex < int.MaxValue; copyIndex++)
        {
            var candidatePath = Path.Combine(
                destinationDirectoryPath,
                $"{sourceNameWithoutExtension} - Copy ({copyIndex}){extension}");
            if (!PathExists(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException($"Could not resolve a unique copy name for: {sourcePath}");
    }

    private static string ResolveDirectoryCreationPath(
        string parentDirectoryPath,
        string preferredName)
    {
        var originalPath = Path.Combine(parentDirectoryPath, preferredName);
        if (!PathExists(originalPath))
        {
            return originalPath;
        }

        for (var copyIndex = 2; copyIndex < int.MaxValue; copyIndex++)
        {
            var candidatePath = Path.Combine(
                parentDirectoryPath,
                $"{preferredName} ({copyIndex})");
            if (!PathExists(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException($"Could not resolve a unique folder name for: {preferredName}");
    }

    private static void EnsureCanCopyIntoDirectory(
        string sourcePath,
        string destinationDirectoryPath)
    {
        if (Directory.Exists(sourcePath) &&
            IsSubPathOf(destinationDirectoryPath, sourcePath))
        {
            throw new IOException("Cannot copy a folder into itself.");
        }
    }

    private static void EnsureCanMoveIntoDirectory(
        string sourcePath,
        string destinationDirectoryPath)
    {
        if (Directory.Exists(sourcePath) &&
            IsSubPathOf(destinationDirectoryPath, sourcePath))
        {
            throw new IOException("Cannot move a folder into itself.");
        }
    }

    private static DirectoryInfo GetExistingDirectory(string directoryPath)
    {
        var normalizedPath = NormalizePath(directoryPath);
        var directory = new DirectoryInfo(normalizedPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {normalizedPath}");
        }

        return directory;
    }

    private static string GetExistingPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = NormalizePath(path);
        if (!PathExists(normalizedPath))
        {
            throw new FileNotFoundException($"File or directory not found: {normalizedPath}", normalizedPath);
        }

        return normalizedPath;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim());
    }

    private static string GetPathName(string path)
    {
        return Path.GetFileName(path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));
    }

    private static bool PathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private static bool IsSamePath(string left, string right)
    {
        return string.Equals(
            NormalizePath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            NormalizePath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubPathOf(string candidatePath, string parentPath)
    {
        var normalizedCandidate = NormalizePath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var normalizedParent = NormalizePath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        return normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct FileTransferTotals(
        int TotalItems,
        long TotalBytes)
    {
        public static FileTransferTotals operator +(
            FileTransferTotals left,
            FileTransferTotals right)
        {
            return new FileTransferTotals(
                left.TotalItems + right.TotalItems,
                left.TotalBytes + right.TotalBytes);
        }
    }

    private sealed class FileOperationProgressState
    {
        private readonly FileOperationKind _operation;
        private readonly IProgress<FileOperationProgress>? _progress;
        private int _completedItems;
        private long _completedBytes;
        private int _totalItems;
        private long _totalBytes;

        public FileOperationProgressState(
            FileOperationKind operation,
            IProgress<FileOperationProgress>? progress)
        {
            _operation = operation;
            _progress = progress;
        }

        public void SetTotals(FileTransferTotals totals)
        {
            _totalItems = totals.TotalItems;
            _totalBytes = totals.TotalBytes;
        }

        public void ReportPreparing()
        {
            Report(FileOperationProgressStage.Preparing);
        }

        public void ReportProcessing()
        {
            Report(FileOperationProgressStage.Processing);
        }

        public void ReportItemStarted(
            string sourcePath,
            string destinationPath)
        {
            Report(
                FileOperationProgressStage.Processing,
                sourcePath,
                destinationPath);
        }

        public void ReportBytesCompleted(
            string sourcePath,
            string destinationPath,
            int completedBytes)
        {
            _completedBytes += completedBytes;
            Report(
                FileOperationProgressStage.Processing,
                sourcePath,
                destinationPath);
        }

        public void ReportItemCompleted(
            string sourcePath,
            string destinationPath,
            long completedBytes)
        {
            _completedItems++;
            _completedBytes += completedBytes;
            Report(
                FileOperationProgressStage.Processing,
                sourcePath,
                destinationPath);
        }

        public void ReportCompleted()
        {
            _completedItems = Math.Max(_completedItems, _totalItems);
            _completedBytes = Math.Max(_completedBytes, _totalBytes);
            Report(FileOperationProgressStage.Completed);
        }

        private void Report(
            FileOperationProgressStage stage,
            string sourcePath = "",
            string destinationPath = "")
        {
            _progress?.Report(new FileOperationProgress
            {
                Operation = _operation,
                Stage = stage,
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                CurrentItemName = GetProgressItemName(sourcePath),
                CompletedItems = _completedItems,
                TotalItems = _totalItems,
                CompletedBytes = _completedBytes,
                TotalBytes = _totalBytes,
            });
        }

        private static string GetProgressItemName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var name = GetPathName(path);

            return string.IsNullOrWhiteSpace(name) ? path : name;
        }
    }

    private static void ValidateFileName(string fileName)
    {
        if (fileName is "." or ".." ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("The name must be a valid file or folder name.", nameof(fileName));
        }
    }
}
