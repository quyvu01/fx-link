using FxLink.Abstractions.Contexts;
using FxLink.Entities;

namespace FxLink.InMemory;

internal interface IInMemoryMessageProcessor<TMessage> where TMessage : class
{
    Task PushMessageAsync(TMessage message, IContext context, CancellationToken token = default);
    IAsyncEnumerable<MessageData<TMessage>> MessagesProcessingAsync();
}