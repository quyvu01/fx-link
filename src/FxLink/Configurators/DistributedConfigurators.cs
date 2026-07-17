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

    public const string RetryCountKey = "x-retry-count";
    public const string TimeToLiveKey = "x-retry-ttl";
    public const string DelayInMsKey = "x-delay-ms";

    public const string ScheduleMessageKey = "x-schedule-message";
    // MessageType
    public const string MessageTypeKey = "x-message-type";
    public const string MessageTypeRetry = "message-retry";
    public const string MessageTypeDeadLetter = "message-dead-letter";
    public const string MessageTypeDelay = "message-delay";
}