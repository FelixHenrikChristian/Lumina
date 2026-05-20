using Lumina.Core.Models;

namespace Lumina.Core.Services;

public interface IFileBrowserService
{
    Task<IReadOnlyList<FileItem>> LoadDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileItem>> LoadDirectoryAsync(
        string directoryPath,
        FileSortOptions sortOptions,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileItem>> SearchDirectoryAsync(
        string directoryPath,
        string query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileItem>> SearchDirectoryAsync(
        string directoryPath,
        string query,
        FileSortOptions sortOptions,
        CancellationToken cancellationToken = default);

    Task<string> CreateDirectoryAsync(
        string parentDirectoryPath,
        string preferredName,
        CancellationToken cancellationToken = default);

    Task<string> RenameAsync(
        string path,
        string newName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IReadOnlyList<string> paths,
        FileDeleteBehavior deleteBehavior,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> CopyAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> MoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default);
}
