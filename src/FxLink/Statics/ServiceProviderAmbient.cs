namespace FxLink.Statics;

public static class ServiceProviderAmbient
{
    private static readonly AsyncLocal<IServiceProvider> AsyncLocal = new();
    internal static void SetServices(IServiceProvider services) => AsyncLocal.Value = services;
    public static IServiceProvider Services => AsyncLocal.Value;
}