using Lumina.Core.Models;

namespace Lumina.Core.Services;

public interface IFileOperationConflictResolver
{
    Task<FileConflictAction> ResolveAsync(
        FileConflictInfo conflict,
        CancellationToken cancellationToken = default);
}
