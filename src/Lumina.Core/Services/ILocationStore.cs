using Lumina.Core.Models;

namespace Lumina.Core.Services;

public interface ILocationStore
{
    Task<IReadOnlyList<Location>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyList<Location> locations,
        CancellationToken cancellationToken = default);
}
