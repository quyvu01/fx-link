using System.Collections.Concurrent;
using System.Text;

namespace FxLink.Nats.Extensions;

internal static class NatsNamingExtensions
{
    private static readonly ConcurrentDictionary<Type, string> MessageNameCache = new();
    private static readonly ConcurrentDictionary<Type, string> ConsumerNameCache = new();

    extension(Type type)
    {
        // Subject suffix for a message type: "namespace.typename". Dots are fine here — they're
        // NATS's own token separator, so the namespace becomes a subject hierarchy.
        internal string GetSubjectName()
        {
            ArgumentNullException.ThrowIfNull(type);
            return MessageNameCache.GetOrAdd(type, static t => SanitizeSubject($"{t.Namespace}.{GetSafeTypeName(t)}"));
        }

        // JetStream durable names can't contain '.', unlike subjects.
        internal string GetConsumerName()
        {
            ArgumentNullException.ThrowIfNull(type);
            return ConsumerNameCache.GetOrAdd(type,
                static t => SanitizeConsumer($"{t.Namespace}.{GetSafeTypeName(t)}"));
        }
    }

    extension(string consumerName)
    {
        // Durable consumer parking a consumer's dead letters — never read from by FxLink itself,
        // it exists so dead-lettered messages are retained (until the stream's MaxAge) instead of
        // being dropped for lack of a consumer that cares.
        internal string DeadLetterConsumerName() => $"{consumerName}-deadletter";
    }

    // Subjects allow '.' as a separator but not wildcards or whitespace; applied to user-supplied
    // custom names too.
    internal static string SanitizeSubject(string name) => Replace(name, keepDot: true);

    // Durable consumer names allow neither dots, wildcards, path separators nor whitespace.
    internal static string SanitizeConsumer(string name) => Replace(name, keepDot: false);

    private static string Replace(string name, bool keepDot)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' || (keepDot && c == '.') ? c : '-');
        return builder.ToString();
    }

    // Unique across closed generics (Fault<A> vs Fault<B> both have raw Name "Fault`1").
    private static string GetSafeTypeName(Type type)
    {
        if (!type.IsGenericType) return type.Name;
        var backtick = type.Name.IndexOf('`');
        var baseName = backtick >= 0 ? type.Name[..backtick] : type.Name;
        return $"{baseName}-{string.Join('-', type.GetGenericArguments().Select(GetSafeTypeName))}";
    }
}
