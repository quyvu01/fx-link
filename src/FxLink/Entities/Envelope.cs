using FxLink.Contexts;

namespace FxLink.Entities;

public record Envelope<TMessage>(TMessage Message, IContext Context) where TMessage : class;

public sealed record ConsumerContextEnvelope<TMessage> where TMessage : class
{
    public TMessage Message { get; set; }
    public ConsumerContextSerializable Context { get; set; }
}

public sealed record ConsumerContextSerializable
{
    public Guid MessageId { get; set; }
    public Guid? RequesterId { get; set; }
    public Guid CorrelationId { get; set; }
    public IHeaders Headers { get; set; }
    public DateTime? SentTime { get; set; }
    public HostInfo HostInfo { get; set; }
    public TimeSpan? TimeToLive { get; set; }
}