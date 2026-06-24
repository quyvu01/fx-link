namespace FxLink.StateMachine.Contexts;

internal static class ContextAmbient
{
    private static readonly AsyncLocal<IServiceProvider> AsyncLocal = new();
    public static void SetServices(IServiceProvider services) => AsyncLocal.Value = services;
    public static IServiceProvider GetServices() => AsyncLocal.Value;
}