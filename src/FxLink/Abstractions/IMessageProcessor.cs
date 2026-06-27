using FxLink.Entities;

namespace FxLink.Abstractions;

internal interface IMessageProcessor<TMessage> where TMessage : class
{
    Task PushMessageAsync(TMessage message, IContext context, CancellationToken token = default);
    IAsyncEnumerable<MessageData<TMessage>> MessagesProcessingAsync();
}