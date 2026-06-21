using System.Collections.Concurrent;
using FxLink.Core.Abstractions;

namespace FxLink.Core.InMemory;

internal sealed record MessageData<TMessage>(TMessage Message, IContext Context, CancellationToken Token)
    where TMessage : class;

internal sealed record DeadLetterMessage<TMessage>(TMessage Message, IContext Context);

internal class InMemoryMessage<TMessage> : IMessageProcessor<TMessage> where TMessage : class
{
    private readonly ConcurrentQueue<MessageData<TMessage>> _inboundMessages = [];
    private readonly ConcurrentQueue<MessageData<TMessage>> _processingMessages = [];
    private readonly ConcurrentQueue<DeadLetterMessage<TMessage>> _deadLetterMessages = [];
    private Func<TMessage, IContext, CancellationToken, Task> _onMessageProcessing;

    public InMemoryMessage()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                if (_inboundMessages.TryDequeue(out var messageData))
                    _processingMessages.Enqueue(messageData);
                await Task.Delay(TimeSpan.FromMilliseconds(1));
            }
        });
        _ = Task.Run(async () =>
        {
            while (true)
            {
                if (_processingMessages.TryDequeue(out var messageData))
                {
                    _ = Task.Run(async () =>
                    {
                        // Temp move to dead letter after 3 failed times
                        var isProcessed = false;
                        var attempt = 0;
                        while (!isProcessed && attempt++ < 3)
                        {
                            try
                            {
                                if (_onMessageProcessing is not null)
                                    await _onMessageProcessing.Invoke(messageData.Message, messageData.Context,
                                        messageData.Token);
                                isProcessed = true;
                            }
                            catch (Exception)
                            {
                                if (attempt == 3)
                                    await MoveToDeadLetterAsync(messageData.Message, messageData.Context);
                            }
                        }
                    });
                }

                await Task.Delay(TimeSpan.FromMilliseconds(1));
            }
        });
    }

    public Task PushMessageAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        _inboundMessages.Enqueue(new MessageData<TMessage>(message, context, token));
        return Task.CompletedTask;
    }

    private Task MoveToDeadLetterAsync(TMessage message, IContext context)
    {
        _deadLetterMessages.Enqueue(new DeadLetterMessage<TMessage>(message, context));
        return Task.CompletedTask;
    }

    public void OnMessageProcessing(Func<TMessage, IContext, CancellationToken, Task> onMessageProcessAsync) =>
        _onMessageProcessing = onMessageProcessAsync;
}