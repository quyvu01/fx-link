using System.Collections.Concurrent;
using System.Threading.Channels;
using FxLink.Abstractions;
using FxLink.Entities;

namespace FxLink.InMemory;

internal class MessageUnPublisherDispatcher
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _lookup = [];

    internal CancellationToken AcquiredToken(Guid tokenId, TimeSpan delay)
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(delay.Add(TimeSpan.FromMilliseconds(10)));
        _lookup.TryAdd(tokenId, cts);
        return cts.Token;
    }

    internal void CancelToken(Guid tokenId)
    {
        if (!_lookup.TryGetValue(tokenId, out var cts)) return;
        cts.Cancel();
        cts.Dispose();
    }
}

internal class InMemoryMessage<TMessage>
    : IMessageProcessor<TMessage> where TMessage : class
{
    private readonly ConcurrentQueue<MessageData<TMessage>> _inboundMessages = [];
    private readonly ConcurrentQueue<MessageData<TMessage>> _processingMessages = [];
    private readonly SemaphoreSlim _processingRing = new(0, 1);
    private readonly SemaphoreSlim _inboundRing = new(0, 1);
    private readonly Channel<MessageData<TMessage>> _channel = Channel.CreateUnbounded<MessageData<TMessage>>();
    private readonly MessageUnPublisherDispatcher _dispatcher;

    private void PushToDelayChannel(MessageData<TMessage> item, TimeSpan delay) => _ = Task.Run(async () =>
    {
        var token = item.Context switch
        {
            IPublisherContext p => p is { ScheduledMessageId: { } scheduledMessageId }
                ? _dispatcher.AcquiredToken(scheduledMessageId, delay)
                : CancellationToken.None,
            _ => CancellationToken.None
        };
        await Task.Delay(delay, token);
        await _channel.Writer.WriteAsync(item, CancellationToken.None);
    });

    public InMemoryMessage(MessageUnPublisherDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await _inboundRing.WaitAsync();
                if (!_inboundMessages.TryDequeue(out var messageData)) continue;
                if (messageData.Context is IPublisherContext { Delay: { } delay } && delay > TimeSpan.Zero)
                {
                    PushToDelayChannel(messageData, delay);
                    continue;
                }

                _processingMessages.Enqueue(messageData);
                _processingRing.Release();
            }
        });
        _ = Task.Run(async () =>
        {
            await foreach (var messageData in _channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                _processingMessages.Enqueue(messageData);
                _processingRing.Release();
            }
        });
    }

    public Task PushMessageAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        if (message is DiscardMessagePublished { ScheduledMessageId: { } scheduledMessageId })
        {
            _dispatcher.CancelToken(scheduledMessageId);
            return Task.CompletedTask;
        }

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