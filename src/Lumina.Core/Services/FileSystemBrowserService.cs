using Lumina.Core.Models;
using Microsoft.VisualBasic.FileIO;

namespace Lumina.Core.Services;

public sealed class FileSystemBrowserService : IFileBrowserService
{
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
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return Task.Run(
            () => LoadDirectory(directoryPath.Trim(), cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<FileItem>> SearchDirectoryAsync(
        string directoryPath,
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(query);

        return Task.Run(
            () => SearchDirectory(directoryPath.Trim(), query.Trim(), cancellationToken),
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
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);

        return Task.Run(
            () => Copy(sourcePaths, destinationDirectoryPath.Trim(), cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<string>> MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);

        return Task.Run(
            () => Move(sourcePaths, destinationDirectoryPath.Trim(), cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<FileItem> LoadDirectory(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        return directory
            .EnumerateFileSystemInfos("*", EnumerationOptions)
            .Select(info =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CreateFileItem(info);
            })
            .OrderByDescending(item => item.IsDirectory)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<FileItem> SearchDirectory(
        string directoryPath,
        string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return LoadDirectory(directoryPath, cancellationToken);
        }

        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        return directory
            .EnumerateFileSystemInfos("*", RecursiveEnumerationOptions)
            .Select(info =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CreateFileItem(info);
            })
            .Where(item => MatchesQuery(item, query))
            .OrderByDescending(item => item.IsDirectory)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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
        CancellationToken cancellationToken)
    {
        var destinationDirectory = GetExistingDirectory(destinationDirectoryPath);
        var copiedPaths = new List<string>(sourcePaths.Count);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedSourcePath = GetExistingPath(sourcePath);
            EnsureCanCopyIntoDirectory(normalizedSourcePath, destinationDirectory.FullName);

            var destinationPath = ResolveCopyDestinationPath(
                normalizedSourcePath,
                destinationDirectory.FullName);
            CopyPath(normalizedSourcePath, destinationPath, cancellationToken);
            copiedPaths.Add(destinationPath);
        }

        return copiedPaths;
    }

    private IReadOnlyList<string> Move(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = GetExistingDirectory(destinationDirectoryPath);
        var movedPaths = new List<string>(sourcePaths.Count);

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
                continue;
            }

            EnsureCanMoveIntoDirectory(normalizedSourcePath, destinationDirectory.FullName);

            if (PathExists(destinationPath))
            {
                throw new IOException($"Destination already exists: {destinationPath}");
            }

            if (Directory.Exists(normalizedSourcePath))
            {
                Directory.Move(normalizedSourcePath, destinationPath);
            }
            else
            {
                File.Move(normalizedSourcePath, destinationPath);
            }

            movedPaths.Add(destinationPath);
        }

        return movedPaths;
    }

    private FileItem CreateFileItem(FileSystemInfo info)
    {
        var isDirectory = info.Attributes.HasFlag(FileAttributes.Directory);
        var name = info.Name;
        var displayName = _tagParserService.GetDisplayName(name);

        return new FileItem
        {
            Name = name,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName,
            Path = info.FullName,
            IsDirectory = isDirectory,
            PreviewKind = isDirectory ? FilePreviewKind.None : ResolvePreviewKind(name),
            Size = isDirectory ? 0 : ((FileInfo)info).Length,
            Modified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            Tags = _tagParserService.ParseTagsFromFilename(name),
        };
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
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(sourcePath))
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var childPath in Directory.EnumerateFileSystemEntries(
                         sourcePath,
                         "*",
                         EnumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();

                CopyPath(
                    childPath,
                    Path.Combine(destinationPath, Path.GetFileName(childPath)),
                    cancellationToken);
            }

            return;
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
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
