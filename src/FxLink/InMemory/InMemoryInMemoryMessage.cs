using System.Collections.Concurrent;
using System.Threading.Channels;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Entities;

namespace FxLink.InMemory;

internal class InMemoryInMemoryMessage<TMessage>
    : IInMemoryMessageProcessor<TMessage> where TMessage : class
{
    private readonly ConcurrentQueue<MessageData<TMessage>> _inboundMessages = [];
    private readonly ConcurrentQueue<MessageData<TMessage>> _processingMessages = [];
    private readonly SemaphoreSlim _processingRing = new(0, 1);
    private readonly SemaphoreSlim _inboundRing = new(0, 1);
    private readonly Channel<MessageData<TMessage>> _channel = Channel.CreateUnbounded<MessageData<TMessage>>();
    private readonly InMemoryMessageUnPublisherDispatcher _dispatcher;

    private void PushToDelayChannel(MessageData<TMessage> item, TimeSpan delay) => _ = Task.Run(async () =>
    {
        var token = CancellationToken.None;
        if (item.Context is IPublisherContext &&
            item.Context.Headers.TryGetValue(DistributedConfigurators.ScheduleMessageKey, out var messageId))
        {
            token = _dispatcher.AcquiredToken(Guid.Parse(messageId.ToString()!), delay);
        }

        await Task.Delay(delay, token);
        await _channel.Writer.WriteAsync(item, CancellationToken.None);
    });

    public InMemoryInMemoryMessage(InMemoryMessageUnPublisherDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await _inboundRing.WaitAsync();
                if (!_inboundMessages.TryDequeue(out var messageData)) continue;
                if (messageData.Context is IPublisherContext publisherContext)
                {
                    var headers = publisherContext.Headers;
                    if (headers.TryGetValue(DistributedConfigurators.MessageTypeKey,
                            out var messageKeyTypeObj) &&
                        messageKeyTypeObj.Equals(DistributedConfigurators.MessageTypeDelay))
                    {
                        if (headers.TryGetValue(DistributedConfigurators.DelayInMsKey, out var delayObject) &&
                            double.TryParse(delayObject.ToString(), out var delay))
                        {
                            PushToDelayChannel(messageData, TimeSpan.FromMilliseconds(delay));
                            continue;
                        }
                    }
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