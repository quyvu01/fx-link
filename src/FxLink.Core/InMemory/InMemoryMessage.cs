using System.Collections.Concurrent;
using FxLink.Core.Abstractions;
using FxLink.Core.Entities;

namespace FxLink.Core.InMemory;

internal sealed record DeadLetterMessage<TMessage>(TMessage Message, IContext Context);

internal class InMemoryMessage<TMessage> : IMessageProcessor<TMessage> where TMessage : class
{
    private readonly ConcurrentQueue<MessageData<TMessage>> _inboundMessages = [];
    private readonly ConcurrentQueue<MessageData<TMessage>> _processingMessages = [];
    private readonly ConcurrentQueue<DeadLetterMessage<TMessage>> _deadLetterMessages = [];
    private readonly SemaphoreSlim _processingRing = new(0, 1);
    private readonly SemaphoreSlim _inboundRing = new(0, 1);

    public InMemoryMessage()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await _inboundRing.WaitAsync();
                if (!_inboundMessages.TryDequeue(out var messageData)) continue;
                _processingMessages.Enqueue(messageData);
                _processingRing.Release();
            }
        });
    }

    public Task PushMessageAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        _inboundMessages.Enqueue(new MessageData<TMessage>(message, context, token));
        _inboundRing.Release();
        return Task.CompletedTask;
    }

    public Task MoveToDeadLetterAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        _deadLetterMessages.Enqueue(new DeadLetterMessage<TMessage>(message, context));
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<DeadLetterMessage<TMessage>>> GetDeadLetterMessagesAsync(
        CancellationToken token = default)
    {
        await Task.Yield();
        return [.._deadLetterMessages];
    }

    public async IAsyncEnumerable<MessageData<TMessage>> MessagesProcessingAsync()
    {
        while (true)
        {
            await _processingRing.WaitAsync();
            if (_processingMessages.TryDequeue(out var messageData))
                yield return messageData;
        }
        // ReSharper disable once IteratorNeverReturns
    }
}