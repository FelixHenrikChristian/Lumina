using Lumina.Core.Models;

namespace Lumina.Core.Services;

public sealed class FileSystemBrowserService : IFileBrowserService
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
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
            Size = isDirectory ? 0 : ((FileInfo)info).Length,
            Modified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            Tags = _tagParserService.ParseTagsFromFilename(name),
        };
    }
}
