using Lumina.Core.Models;

namespace Lumina.Core.Services;

public interface ITagGroupStore
{
    Task<IReadOnlyList<TagGroup>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyList<TagGroup> tagGroups,
        CancellationToken cancellationToken = default);
}
