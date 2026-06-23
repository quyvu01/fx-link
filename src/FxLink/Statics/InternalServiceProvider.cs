namespace FxLink.Statics;

internal static class InternalServiceProvider
{
    private static readonly AsyncLocal<IServiceProvider> AsyncLocal = new();

    internal static void SetServices(IServiceProvider services) => AsyncLocal.Value = services;
    internal static IServiceProvider Services => AsyncLocal.Value;
}