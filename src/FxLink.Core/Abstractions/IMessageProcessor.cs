namespace FxLink.Core.Abstractions;

internal interface IMessageProcessor<TMessage> where TMessage : class
{
    Task PushMessageAsync(TMessage message, IContext context, CancellationToken token = default);
    void OnMessageProcessing(Func<TMessage, IContext, CancellationToken, Task> onMessageProcessAsync);
}