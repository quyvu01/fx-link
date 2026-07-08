using System.Threading.Channels;

namespace FxLink.RabbitMq.Abstractions;

internal record MessageData(
    byte[] MessageBody,
    string MessageType,
    Guid CorrelationId,
    Dictionary<string, object> Headers,
    CancellationToken Token = default);

internal interface IConsumeMessage
{
    Channel<MessageData> MessageChannel();
}