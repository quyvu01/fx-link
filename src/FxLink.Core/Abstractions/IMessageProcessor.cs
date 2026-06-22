using FxLink.Core.Entities;
using FxLink.Core.InMemory;

namespace FxLink.Core.Abstractions;

internal interface IMessageProcessor<TMessage> where TMessage : class
{
    Task PushMessageAsync(TMessage message, IContext context, CancellationToken token = default);
    IAsyncEnumerable<MessageData<TMessage>> MessagesProcessingAsync();
    Task MoveToDeadLetterAsync(TMessage message, IContext context, CancellationToken token = default);
    Task<IReadOnlyCollection<DeadLetterMessage<TMessage>>> GetDeadLetterMessagesAsync(CancellationToken token = default);
}