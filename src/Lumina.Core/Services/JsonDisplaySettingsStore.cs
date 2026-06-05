using Lumina.Core.Models;

namespace Lumina.Core.Services;

public sealed class JsonDisplaySettingsStore : IDisplaySettingsStore
{
    public const string FileName = "settings.json";

    private readonly ISettingsStore _settingsStore;

    public JsonDisplaySettingsStore()
        : this(new JsonSettingsStore())
    {
    }

    public JsonDisplaySettingsStore(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public async Task<DisplaySettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsStore.LoadAsync(
            FileName,
            new DisplaySettings(),
            cancellationToken);

        return settings with
        {
            Language = DisplayLanguage.Normalize(settings.Language),
        };
    }

    public Task SaveAsync(DisplaySettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return _settingsStore.SaveAsync(
            FileName,
            settings with
            {
                Language = DisplayLanguage.Normalize(settings.Language),
            },
            cancellationToken);
    }
}
