using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Microsoft.Extensions.Logging;

namespace FxLink.RabbitMq.Implementations;

internal class RabbitMqRetryPolicy<TMessage>(ILogger<RabbitMqRetryPolicy<TMessage>> logger)
    : IRetryPolicyHandling<TMessage> where TMessage : class
{
    public async Task HandleRetryPolicyAsync(IConsumerContext<TMessage> ctx, CancellationToken token = default)
    {
        logger.LogInformation("Have some retried, but failed");
        ctx.Retried();
        await ctx.PublishAsync(ctx.Message, new PublisherContext(ctx), token);
    }

    public Task HandleDeadLetterAsync(IConsumerContext<TMessage> ctx, CancellationToken token = default)
    {
        logger.LogError("Have some retried, but dead letter queue");
        return Task.CompletedTask;
    }
}