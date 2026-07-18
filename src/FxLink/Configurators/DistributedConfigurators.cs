using System.Text.Json;

namespace FxLink.Configurators;

public static class DistributedConfigurators
{
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static readonly TimeSpan[] DefaultRetryPolicy =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];

    public static class Headers
    {
        public const string RetryCountKey = "x-retry-count";
        public const string TimeToLiveKey = "x-retry-ttl";
        public const string DelayInMsKey = "x-delay-ms";
        public const string ScheduleMessageKey = "x-schedule-message";
        public const string MessageTypeKey = "x-message-type";

        public const string ExceptionTypeKey = "x-exception-type";
        public const string ExceptionMessageKey = "x-exception-message";
        public const string ExceptionStackTraceKey = "x-exception-stacktrace";
    }

    public static class MessageTypes
    {
        public const string Retry = "message-retry";
        public const string DeadLetter = "message-dead-letter";
        public const string Delay = "message-delay";
    }
}