namespace FxLink.RabbitMq.Constants;

internal static class RabbitMqConstants
{
    internal const string DefaultUserName = "guest";
    internal const string DefaultPassword = "guest";
    internal const int DefaultPort = 5672;
    internal static readonly ushort DefaultPoolSize = (ushort)Math.Min(Environment.ProcessorCount, 8);
    internal static readonly ushort DefaultPrefetchCount = (ushort)Math.Min(Environment.ProcessorCount * 2, 16);
    internal static ushort DefaultConcurrentMessageLimit => DefaultPrefetchCount;
}