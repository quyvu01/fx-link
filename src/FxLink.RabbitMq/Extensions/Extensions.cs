using System.Collections.Concurrent;
using FxLink.Faults;

namespace FxLink.RabbitMq.Extensions;

internal static class Extensions
{
    private static readonly ConcurrentDictionary<Type, string> ConsumerNameCache = new();
    private static readonly ConcurrentDictionary<Type, string> ExchangeNameCache = new();

    extension(Type consumerType)
    {
        internal string GetRetryExchangeName() => $"{consumerType.GetExchangeName()}.retry";
        internal string GetDeadLetterExchangeName() => $"{consumerType.GetExchangeName()}.dlq";
        internal string GetRetryConsumer() => $"{consumerType.GetConsumerName()}.retry";
        internal string GetDeadLetterConsumer() => $"{consumerType.GetConsumerName()}.dlq";

        internal string GetConsumerName()
        {
            ArgumentNullException.ThrowIfNull(consumerType);
            return ConsumerNameCache.GetOrAdd(consumerType,
                static t => $"{t.Namespace}-{t.Name}".Replace('.', '-').ToLower());
        }

        internal string GetExchangeName()
        {
            ArgumentNullException.ThrowIfNull(consumerType);
            return ExchangeNameCache.GetOrAdd(consumerType, static t =>
            {
                if (!typeof(Fault).IsAssignableFrom(t)) return $"{t!.Namespace}:{GetSafeTypeName(t)}";
                if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(Fault<>))
                    return $"{t.Namespace}:{nameof(Fault)}";
                var innerMessageType = t.GetGenericArguments()[0];
                return $"{t.Namespace}:{nameof(Fault)}-{GetSafeTypeName(innerMessageType)}";
            });
        }
    }

    // Builds a name that stays unique across closed generic types (e.g. Fault<OrderCreated> vs
    // Fault<OrderCancelled> both have the raw Name "Fault`1" — the backtick/arity marker alone
    // doesn't disambiguate them), recursing into nested generic arguments. Not cached directly:
    // it only ever runs behind ExchangeNameCache's GetOrAdd, so it already executes once per type.
    private static string GetSafeTypeName(Type type)
    {
        if (!type.IsGenericType) return type.Name;
        var backtickIndex = type.Name.IndexOf('`');
        var baseName = backtickIndex >= 0 ? type.Name[..backtickIndex] : type.Name;
        var genericArgNames = type.GetGenericArguments().Select(GetSafeTypeName);
        return $"{baseName}-{string.Join('-', genericArgNames)}";
    }
}