using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Service1.RabbitMqTests;

namespace Service1.Consumers.RabbitMqTests;

public class RabbitMqTestConsumers(ILogger<RabbitMqTestConsumers> logger) : IConsumer<RabbitMqTestPublisher>
{
    public Task ConsumeAsync(IConsumerContext<RabbitMqTestPublisher> context, CancellationToken token = default)
    {
        logger.LogInformation("Logged message: {@Message}", context.Message);
        return Task.CompletedTask;
    }
}