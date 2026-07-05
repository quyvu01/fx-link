using FxLink.Entities;

namespace FxLink.Statics;

public static class ConsumerAmbient
{
    private static readonly AsyncLocal<ConsumerAmbientData> AsyncLocal = new();

    internal static void SetConsumerAmbientData(IServiceProvider services, Type consumerType) =>
        AsyncLocal.Value = new ConsumerAmbientData(services, consumerType);

    public static IServiceProvider Services => AsyncLocal.Value.Services;
    public static Type ConsumerType => AsyncLocal.Value.ConsumerType;
}