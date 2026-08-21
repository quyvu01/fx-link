using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Registries;

public class ConfiguratorBatchConsumerTests
{
    private interface IBatchedMessage;

    private sealed class BatchedMessageConsumer : IConsumer<IBatch<IBatchedMessage>>
    {
        public Task ConsumeAsync(IConsumeContext<IBatch<IBatchedMessage>> context, CancellationToken token = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void AddConsumer_registers_the_unwrapped_message_type_for_a_batch_consumer_not_IBatch()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        IMessageKeys messageKeys = null;
        services.AddFxLink(cfg =>
        {
            cfg.UseInMemory();
            cfg.AddConsumer<BatchedMessageConsumer>();
            messageKeys = cfg.MessageKeys();
        });

        // The wire/transport-facing key must be the original message type — IBatch<T> is an
        // in-process-only accumulation wrapper and is never published, so a transport driven by
        // IMessageKeys (e.g. RabbitMqClient.StartAsync) would otherwise try to subscribe to a type
        // that can never arrive.
        messageKeys.GetKeysByMessageType(typeof(IBatchedMessage))
            .ShouldContain(typeof(BatchedMessageConsumer));

        messageKeys.GetKeysByMessageType(typeof(IBatch<IBatchedMessage>))
            .ShouldBeEmpty();
    }
}
