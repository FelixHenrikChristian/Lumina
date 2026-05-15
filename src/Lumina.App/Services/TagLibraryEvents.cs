namespace Lumina.App.Services;

public static class TagLibraryEvents
{
    public static event EventHandler? Imported;

    public static void RaiseImported()
    {
        Imported?.Invoke(null, EventArgs.Empty);
    }
}
