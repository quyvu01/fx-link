using System.Text;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Entities;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Entities;
using FxLink.RabbitMq.Extensions;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FxLink.RabbitMq.Implementations;

internal abstract class AbstractRabbitMqConnector
{
    public abstract Task ProcessMessageReceivedAsync(BasicDeliverEventArgs args, Type consumerType);
    public abstract Task ProcessResponseMessageAsync(BasicDeliverEventArgs args);
}

internal class RabbitMqClientConnector<TMessage>(
    IRabbitMqClient client,
    IServiceProvider services,
    IInMemoryResponseSetter inMemoryResponseSetter) :
    AbstractRabbitMqConnector, IClientConnector<TMessage> where TMessage : class
{
    private readonly IDelayMessageProvider _delayMessageProvider = services.GetService<IDelayMessageProvider>();

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        var messageKind = context.Headers.Get<string>(DistributedConfigurators.Headers.MessageKindKey);

        if (messageKind == DistributedConfigurators.MessageKinds.Delay)
        {
            if (_delayMessageProvider is null)
                throw new InvalidOperationException(
                    $"Cannot send a delayed {typeof(TMessage).Name}: no IDelayMessageProvider is registered. " +
                    "Call IConfigurator.UseRabbitMqDelayScheduler() (or register a custom IDelayMessageProvider) " +
                    "before publishing delayed messages.");
            var delayInMs = (long)context.Headers.Get<double>(DistributedConfigurators.Headers.DelayInMsKey);
            await _delayMessageProvider.PublishDelayedAsync(message, context, delayInMs, token);
            return;
        }

        var props = new BasicProperties
            { CorrelationId = context.CorrelationId.ToString(), Type = typeof(TMessage).AssemblyQualifiedName };
        if (context is IRequestContext) props.ReplyTo = client.ReplyQueueName;
        if (messageKind == DistributedConfigurators.MessageKinds.Retry)
            props.Expiration = ((long)context.Headers.Get<double>(DistributedConfigurators.Headers.TimeToLiveKey))
                .ToString();

        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var messageBytes = Encoding.UTF8.GetBytes(serializedMessage);
        var routingKey = string.Empty;
        if (context is IResponseContext responseContext)
            routingKey = responseContext.Headers.Get<string>(DistributedConfigurators.Headers.ReplyToKey);

        var exchangeName = GetExchangeName(context, messageKind);
        await client.PublishMessageAsync(new MessagePublisher(exchangeName, routingKey, props, messageBytes), token);
    }

    private static string GetExchangeName(IContext context, string messageKind)
    {
        if (context is IResponseContext) return string.Empty;
        return messageKind switch
        {
            DistributedConfigurators.MessageKinds.Retry => typeof(TMessage).GetRetryExchangeName(),
            DistributedConfigurators.MessageKinds.DeadLetter => typeof(TMessage).GetDeadLetterExchangeName(),
            _ => typeof(TMessage).GetExchangeName()
        };
    }

    public override async Task ProcessMessageReceivedAsync(BasicDeliverEventArgs args, Type consumerType)
    {
        var messageType = args.BasicProperties.Type;
        if (typeof(TMessage).AssemblyQualifiedName != messageType) return;
        var bodyAsJson = Encoding.UTF8.GetString(args.Body.Span);
        var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<TMessage>>(bodyAsJson,
            DistributedConfigurators.JsonSerializerOptions);

        if (envelope is null) return;

        using var scope = services.CreateScope();
        var serverConnector = scope.ServiceProvider.GetRequiredService<IConsumerConnector<TMessage>>();
        var headers = envelope.Context.Headers;
        if (args.BasicProperties.ReplyTo is { Length: > 0 } replyTo)
            headers.Set(DistributedConfigurators.Headers.ReplyToKey, replyTo);
        var consumerContext = new ConsumerContext<TMessage>(envelope.Message, envelope.Context.RequesterId,
            envelope.Context.CorrelationId, headers);
        await serverConnector.ConsumeAsync(consumerContext, consumerType, args.CancellationToken);
    }

    public override async Task ProcessResponseMessageAsync(BasicDeliverEventArgs args)
    {
        await Task.Yield();
        var messageType = args.BasicProperties.Type;
        if (typeof(Result).AssemblyQualifiedName != messageType) return;
        var bodyAsJson = Encoding.UTF8.GetString(args.Body.Span);
        var envelope = JsonSerializer.Deserialize<ConsumerContextEnvelope<Result>>(bodyAsJson,
            DistributedConfigurators.JsonSerializerOptions);
        if (envelope?.Context.RequesterId is not { } requesterId) return;
        inMemoryResponseSetter.TrySetResult(requesterId, new MessageData<Result>(envelope.Message,
            new ResponseContext(requesterId, envelope.Context.CorrelationId, envelope.Context.Headers),
            args.CancellationToken));
    }
}