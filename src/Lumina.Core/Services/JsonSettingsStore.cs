using System.Text.Json;

namespace Lumina.Core.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public JsonSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lumina"))
    {
    }

    public JsonSettingsStore(string appDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);

        AppDataDirectory = appDataDirectory;
    }

    public string AppDataDirectory { get; }

    public async Task<T> LoadAsync<T>(
        string fileName,
        T fallbackValue,
        CancellationToken cancellationToken = default)
    {
        var path = GetPath(fileName);
        if (!File.Exists(path))
        {
            return fallbackValue;
        }

        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(
            stream,
            SerializerOptions,
            cancellationToken);

        return value is null ? fallbackValue : value;
    }

    public async Task SaveAsync<T>(
        string fileName,
        T value,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppDataDirectory);

        var path = GetPath(fileName);
        var temporaryPath = $"{path}.tmp";

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                value,
                SerializerOptions,
                cancellationToken);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    private string GetPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException("Only plain file names are allowed.", nameof(fileName));
        }

        return Path.Combine(AppDataDirectory, fileName);
    }
}
