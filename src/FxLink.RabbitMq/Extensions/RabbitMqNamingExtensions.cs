using System.Collections.Concurrent;
using FxLink.Faults;

namespace FxLink.RabbitMq.Extensions;

internal static class RabbitMqNamingExtensions
{
    private static readonly ConcurrentDictionary<Type, string> ConsumerNameCache = new();
    private static readonly ConcurrentDictionary<Type, string> ExchangeNameCache = new();

    // Applies to both consumer types and message types — RabbitMqClient calls these on the
    // consumer's Type for queue/consumer names, and on each message's Type for exchange names.
    extension(Type type)
    {
        internal string GetRetryExchangeName() => $"{type.GetExchangeName()}.retry";
        internal string GetDeadLetterExchangeName() => $"{type.GetExchangeName()}.deadletter";
        internal string GetDelayExchangeName() => $"{type.GetExchangeName()}.delay";
        internal string GetConsumerName()
        {
            ArgumentNullException.ThrowIfNull(type);
            return ConsumerNameCache.GetOrAdd(type,
                static t => $"{t.Namespace}.{t.Name}".ToLower());
        }

        internal string GetExchangeName()
        {
            ArgumentNullException.ThrowIfNull(type);
            return ExchangeNameCache.GetOrAdd(type, static t =>
            {
                if (!typeof(Fault).IsAssignableFrom(t)) return $"{t!.Namespace}.{GetSafeTypeName(t)}".ToLower();
                if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(Fault<>))
                    return $"{t.Namespace}.{nameof(Fault)}".ToLower();
                var innerMessageType = t.GetGenericArguments()[0];
                return $"{t.Namespace}.{nameof(Fault)}.{GetSafeTypeName(innerMessageType)}".ToLower();
            });
        }
    }

    extension(string consumerName)
    {
        internal string DeadLetterQueueName() => $"{consumerName}.deadletter";

        internal string RetryConsumerName(Type exchangeType) =>
            $"{consumerName}.retry.{exchangeType.GetExchangeName().Split('.').LastOrDefault()}";
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