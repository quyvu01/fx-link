using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;

namespace FxLink.RabbitMq.Implementations;

internal class RabbitMqRetryPolicy<TMessage>
    : IRetryPolicyHandling<TMessage> where TMessage : class
{
    public async Task HandleRetryPolicyAsync(IConsumerContext<TMessage> ctx, CancellationToken token = default) =>
        await ctx.PublishAsync(ctx.Message, new PublisherContext(ctx), token);

    public async Task HandleDeadLetterAsync(IConsumerContext<TMessage> ctx, CancellationToken token = default) =>
        await ctx.PublishAsync(ctx.Message, new PublisherContext(ctx), token);
}