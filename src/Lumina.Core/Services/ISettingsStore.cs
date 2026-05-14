namespace Lumina.Core.Services;

public interface ISettingsStore
{
    string AppDataDirectory { get; }

    Task<T> LoadAsync<T>(string fileName, T fallbackValue, CancellationToken cancellationToken = default);

    Task SaveAsync<T>(string fileName, T value, CancellationToken cancellationToken = default);
}
