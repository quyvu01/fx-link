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
        public const string TimeToLiveKey = "x-retry-ttl";
        public const string DelayInMsKey = "x-delay-ms";
        public const string ScheduleMessageKey = "x-schedule-message";
        public const string MessageKindKey = "x-message-type";
        public const string ReplyToKey = "x-reply-to";

        public const string ExceptionTypeKey = "x-exception-type";
        public const string ExceptionMessageKey = "x-exception-message";
        public const string ExceptionStackTraceKey = "x-exception-stacktrace";
    }

    public static class MessageKinds
    {
        public const string Retry = "message-retry";
        public const string DeadLetter = "message-dead-letter";
        public const string Delay = "message-delay";
    }
}