using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FxLink.Azure.ServiceBus.Constants;

namespace FxLink.Azure.ServiceBus.Extensions;

internal static class ServiceBusNamingExtensions
{
    private static readonly ConcurrentDictionary<Type, string> TopicNameCache = new();
    private static readonly ConcurrentDictionary<Type, string> SubscriptionNameCache = new();

    extension(Type type)
    {
        // One topic per message type: "namespace.typename".
        internal string GetTopicName()
        {
            ArgumentNullException.ThrowIfNull(type);
            return TopicNameCache.GetOrAdd(type, static t => SanitizeTopic($"{t.Namespace}.{GetSafeTypeName(t)}"));
        }

        // One subscription per consumer type, same name on every topic it consumes.
        internal string GetSubscriptionName()
        {
            ArgumentNullException.ThrowIfNull(type);
            return SubscriptionNameCache.GetOrAdd(type,
                static t => SanitizeSubscription($"{t.Namespace}.{GetSafeTypeName(t)}"));
        }
    }

    // Entity names allow letters, digits, '-', '_' and '.'; applied to user-supplied custom names too.
    internal static string SanitizeTopic(string name)
    {
        var sanitized = Replace(name);
        return sanitized.Length <= ServiceBusConstants.MaxTopicNameLength
            ? sanitized
            : Shorten(sanitized, ServiceBusConstants.MaxTopicNameLength);
    }

    // Subscription names are capped at 50 characters. A longer name is cut and suffixed with a hash of the
    // full (sanitised) name, so two long names sharing a prefix never collide and the result is stable.
    internal static string SanitizeSubscription(string name)
    {
        var sanitized = Replace(name);
        return sanitized.Length <= ServiceBusConstants.MaxSubscriptionNameLength
            ? sanitized
            : Shorten(sanitized, ServiceBusConstants.MaxSubscriptionNameLength);
    }

    private static string Shorten(string name, int maxLength)
    {
        const int hashLength = 8;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..hashLength].ToLowerInvariant();
        return $"{name[..(maxLength - hashLength - 1)]}-{hash}";
    }

    private static string Replace(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-');
        return builder.ToString();
    }

    // Unique across closed generics (Fault<A> vs Fault<B> both have the raw Name "Fault`1").
    private static string GetSafeTypeName(Type type)
    {
        if (!type.IsGenericType) return type.Name;
        var backtick = type.Name.IndexOf('`');
        var baseName = backtick >= 0 ? type.Name[..backtick] : type.Name;
        return $"{baseName}-{string.Join('-', type.GetGenericArguments().Select(GetSafeTypeName))}";
    }
}
