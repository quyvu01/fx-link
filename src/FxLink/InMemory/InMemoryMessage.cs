using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.Entities;

namespace FxLink.InMemory;

internal class InMemoryMessage<TMessage> : IMessageProcessor<TMessage> where TMessage : class
{
    private readonly ConcurrentQueue<MessageData<TMessage>> _inboundMessages = [];
    private readonly ConcurrentQueue<MessageData<TMessage>> _processingMessages = [];
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