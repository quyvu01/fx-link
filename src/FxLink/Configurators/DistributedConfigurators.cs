using System.Text.Json;
using FxLink.Serialization;

namespace FxLink.Configurators;

public static class DistributedConfigurators
{
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // HeadersJsonConverter must come first: it's an exact match for IHeaders, whereas
        // InterfaceMessageJsonConverterFactory.CanConvert accepts ANY non-corlib interface type
        // (including IHeaders) and would otherwise claim it first and fail — IHeaders declares
        // methods (Get/Set/TryGetHeader), which the message-contract proxy builder rejects.
        Converters = { new HeadersJsonConverter(), new InterfaceMessageJsonConverterFactory() }
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