using FxLink.Entities;

namespace FxLink.Statics;

internal static class ConsumerAmbient
{
    private static readonly AsyncLocal<ConsumerAmbientData> AsyncLocal = new();

    internal static void SetConsumerAmbientData(IServiceProvider services, Type consumerType) =>
        AsyncLocal.Value = new ConsumerAmbientData(services, consumerType);

    internal static IServiceProvider Services => AsyncLocal.Value.Services;
    internal static Type ConsumerType => AsyncLocal.Value.ConsumerType;
}