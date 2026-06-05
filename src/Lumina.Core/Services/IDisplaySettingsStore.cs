using Lumina.Core.Models;

namespace Lumina.Core.Services;

public interface IDisplaySettingsStore
{
    Task<DisplaySettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DisplaySettings settings, CancellationToken cancellationToken = default);
}
