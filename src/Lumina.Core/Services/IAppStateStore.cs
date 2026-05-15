using Lumina.Core.Models;

namespace Lumina.Core.Services;

public interface IAppStateStore
{
    Task<AppState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppState appState, CancellationToken cancellationToken = default);
}
