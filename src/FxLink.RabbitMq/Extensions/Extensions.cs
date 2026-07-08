using FxLink.Faults;

namespace FxLink.RabbitMq.Extensions;

internal static class Extensions
{
    extension(Type consumerType)
    {
        internal string GetConsumerName() =>
            $"{consumerType.Namespace}-{consumerType.Name}".Replace('.', '-').ToLower();

        // Todo: check more edge case for Message exchange name
        internal string GetExchangeName()
        {
            ArgumentNullException.ThrowIfNull(consumerType);
            if (!typeof(Fault).IsAssignableFrom(consumerType)) return $"{consumerType.Namespace}:{consumerType.Name}";
            if (!consumerType.IsGenericType || consumerType.GetGenericTypeDefinition() != typeof(Fault<>))
                return $"{consumerType.Namespace}:{nameof(Fault)}";
            var innerMessageType = consumerType.GetGenericArguments()[0];
            return $"{consumerType.Namespace}:{nameof(Fault)}-{innerMessageType.Name}";
        }
    }
}