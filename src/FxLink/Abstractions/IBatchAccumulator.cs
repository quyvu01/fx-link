using FxLink.Contexts;

namespace FxLink.Abstractions;

// Owned by ConsumerPipelineBehaviorOrchestrator<TMessage> for a message type that has batching
// configured. Implementations must return quickly — AddAsync only buffers the message (and, once a
// flush condition is met, kicks off dispatch in the background); it must NOT await the actual
// IConsumer<IBatch<TMessage>> execution, since the caller's ack is gated on this call returning.
internal interface IBatchAccumulator<in TMessage> where TMessage : class
{
    Task AddAsync(IConsumeContext<TMessage> context, CancellationToken token = default);
}
