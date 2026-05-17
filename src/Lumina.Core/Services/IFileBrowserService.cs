using Lumina.Core.Models;

namespace Lumina.Core.Services;

public interface IFileBrowserService
{
    Task<IReadOnlyList<FileItem>> LoadDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);
}
