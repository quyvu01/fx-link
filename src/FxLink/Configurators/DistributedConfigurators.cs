using System.Text.Json;
using FxLink.Serialization;

namespace FxLink.Configurators;

public static class DistributedConfigurators
{
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // HeadersJsonConverter and ContextJsonConverter must come first: they're exact matches for
        // IHeaders/IContext, whereas InterfaceMessageJsonConverterFactory.CanConvert accepts ANY
        // non-corlib interface type (including these two) and would otherwise claim them first —
        // IHeaders declares methods (Get/Set/TryGetHeader), which the message-contract proxy builder
        // rejects, and IContext would serialize by its 4-member contract instead of the runtime type.
        Converters = { new HeadersJsonConverter(), new ContextJsonConverter(), new InterfaceMessageJsonConverterFactory() }
    };

    internal static readonly TimeSpan[] DefaultRetryPolicy =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];

    public static class Headers
    {
        public const string RetryCountKey = "x-retry-count";
        public const string DeliveryKindKey = "x-delivery-kind";
        public const string RequestSemanticsKey = "x-request-kind";
        public const string MessageRoutingKey = "x-message-routing-key";
        public const string ReplyToKey = "x-reply-to";

        public const string ExceptionTypeKey = "x-exception-type";
        public const string ExceptionMessageKey = "x-exception-message";
        public const string ExceptionStackTraceKey = "x-exception-stacktrace";
    }

    public static class DeliveryKinds
    {
        public const string Retry = "delivery-retry";
        public const string DeadLetter = "delivery-dead-letter";
        public const string Delay = "delivery-delay";
    }

    public static class RequestSemantics
    {
        public const string RequestAsPublisher = "request-as-publisher";
    }
}