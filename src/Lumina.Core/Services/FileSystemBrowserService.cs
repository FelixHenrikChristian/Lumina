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

    public Task<string> CreateDirectoryAsync(
        string parentDirectoryPath,
        string preferredName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredName);

        return Task.Run(
            () => CreateDirectory(parentDirectoryPath.Trim(), preferredName.Trim(), cancellationToken),
            cancellationToken);
    }

    public Task<string> RenameAsync(
        string path,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        return Task.Run(
            () => Rename(path.Trim(), newName.Trim(), cancellationToken),
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

    public Task<IReadOnlyList<string>> CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);

        return Task.Run(
            () => Copy(sourcePaths, destinationDirectoryPath.Trim(), progress, cancellationToken),
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

    public Task<IReadOnlyList<string>> MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);

        return Task.Run(
            () => Move(sourcePaths, destinationDirectoryPath.Trim(), progress, cancellationToken),
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

    private IReadOnlyList<string> Copy(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = GetExistingDirectory(destinationDirectoryPath);
        var copiedPaths = new List<string>(sourcePaths.Count);
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

            var destinationPath = ResolveCopyDestinationPath(
                normalizedSourcePath,
                destinationDirectory.FullName);
            CopyPath(normalizedSourcePath, destinationPath, progressState, cancellationToken);
            copiedPaths.Add(destinationPath);
        }

        progressState.ReportCompleted();

        return copiedPaths;
    }

    private IReadOnlyList<string> Move(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = GetExistingDirectory(destinationDirectoryPath);
        var movedPaths = new List<string>(sourcePaths.Count);
        var progressState = CreateProgressState(
            FileOperationKind.Move,
            sourcePaths,
            progress,
            cancellationToken);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedSourcePath = GetExistingPath(sourcePath);
            var destinationPath = Path.Combine(
                destinationDirectory.FullName,
                GetPathName(normalizedSourcePath));

            if (IsSamePath(normalizedSourcePath, destinationPath))
            {
                movedPaths.Add(normalizedSourcePath);
                progressState.ReportItemCompleted(
                    normalizedSourcePath,
                    normalizedSourcePath,
                    completedBytes: 0);
                continue;
            }

            EnsureCanMoveIntoDirectory(normalizedSourcePath, destinationDirectory.FullName);

            if (PathExists(destinationPath))
            {
                throw new IOException($"Destination already exists: {destinationPath}");
            }

            progressState.ReportItemStarted(normalizedSourcePath, destinationPath);
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(normalizedSourcePath))
            {
                Directory.Move(normalizedSourcePath, destinationPath);
            }
            else
            {
                File.Move(normalizedSourcePath, destinationPath);
            }

            movedPaths.Add(destinationPath);
            progressState.ReportItemCompleted(
                normalizedSourcePath,
                destinationPath,
                completedBytes: 0);
        }

        progressState.ReportCompleted();

        return movedPaths;
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
        CancellationToken cancellationToken)
    {
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

                    CopyPath(
                        childPath,
                        Path.Combine(destinationPath, Path.GetFileName(childPath)),
                        progressState,
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

            return;
        }

        CopyFile(sourcePath, destinationPath, progressState, cancellationToken);
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
