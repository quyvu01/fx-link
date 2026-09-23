using System.Collections.Concurrent;

namespace FxLink.Aws.Sqs.Extensions;

internal static class SqsNamingExtensions
{
    private static readonly ConcurrentDictionary<Type, string> TopicNameCache = new();
    private static readonly ConcurrentDictionary<Type, string> QueueNameCache = new();

    // Applies to both message types (topic names) and consumer types (queue names) — SNS/SQS
    // names only allow alphanumerics, hyphens and underscores (no dots), unlike AMQP's exchange/
    // queue names, so "Namespace.TypeName" has to be re-punctuated rather than reused verbatim.
    extension(Type type)
    {
        // One SNS topic per message type — the fan-out point, mirrors RabbitMq's fanout exchange.
        internal string GetTopicName()
        {
            ArgumentNullException.ThrowIfNull(type);
            return TopicNameCache.GetOrAdd(type, static t => Sanitize($"{t.Namespace}.{t.Name}"));
        }

        // One SQS queue per consumer type — subscribed to every topic it consumes.
        internal string GetQueueName()
        {
            ArgumentNullException.ThrowIfNull(type);
            return QueueNameCache.GetOrAdd(type, static t => Sanitize($"{t.Namespace}.{t.Name}"));
        }
    }

    // Also run over user-supplied custom names (IMessageConfigurator.Name(...),
    // ReceivedEndpoint(...)) — a name that's valid on another transport, or that simply contains
    // dots, would otherwise fail CreateTopic/CreateQueue outright instead of just being renamed.
    internal static string Sanitize(string name) => name.Replace('.', '-').ToLower();
}
