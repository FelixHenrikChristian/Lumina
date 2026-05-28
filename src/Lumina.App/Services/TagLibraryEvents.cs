namespace Lumina.App.Services;

public static class TagLibraryEvents
{
    public static event EventHandler? Imported;

    public static event EventHandler? Changed;

    public static void RaiseImported()
    {
        Imported?.Invoke(null, EventArgs.Empty);
        RaiseChanged();
    }

    public static void RaiseChanged()
    {
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
