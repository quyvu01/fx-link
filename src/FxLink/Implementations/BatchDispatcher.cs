using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

// The real flushHandler BatchAccumulator<TMessage> is wired up with in production (see the DI
// registration that creates it). Kept separate from BatchAccumulator itself so the buffering/timer
// logic stays testable without a real DI container — see BatchAccumulatorTests.
internal sealed class BatchDispatcher<TMessage>(IServiceProvider rootServiceProvider) where TMessage : class
{
    public async Task DispatchAsync(IReadOnlyList<IConsumeContext<TMessage>> messages, Type consumerType,
        CancellationToken token = default)
    {
        // A fresh scope per flush, never a message's own scope — that scope may already be
        // disposed by the time this runs (AddAsync returns before the batch is even full), and a
        // batch consume also has nothing to do with any single originating message's lifetime.
        using var scope = rootServiceProvider.CreateScope();

        var batch = new MessageBatch<TMessage>(messages);
        // No single message in the batch owns this identity — CorrelationId/MessageId are new,
        // matching how PublishContext.New()/RequestContext.New() mint fresh ids for context objects
        // that don't originate from an existing IContext.
        var batchContext = new ConsumeContext<IBatch<TMessage>>(batch, new HeaderBag(), Id.New(), requesterId: null);

        var connector = scope.ServiceProvider.GetRequiredService<IConsumerConnector<IBatch<TMessage>>>();
        await connector.ConsumeAsync(batchContext, consumerType, token);
    }
}
