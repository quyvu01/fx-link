using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Service1.RabbitMqTests;

namespace Service1.Consumers.RabbitMqTests;

public class RabbitMqTestConsumerWithDefinition(ILogger<RabbitMqTestConsumerWithDefinition> logger) :
    IConsumer<RabbitMqTestPublisher>,
    IConsumer<RabbitMqTestRetry>
{
    public async Task ConsumeAsync(IConsumerContext<RabbitMqTestRetry> context, CancellationToken token = default)
    {
        await Task.Yield();
        logger.LogInformation("Test retry!!!");
        throw new InvalidDataException("Test some invalid exception");
    }

    public Task ConsumeAsync(IConsumerContext<RabbitMqTestPublisher> context, CancellationToken token = default)
    {
        logger.LogInformation("Test retry!!!");
        return Task.CompletedTask;
    }
}