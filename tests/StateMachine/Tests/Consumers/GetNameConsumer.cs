using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using StateMachine.Tests.Events;

namespace StateMachine.Tests.Consumers;

public sealed class GetNameConsumer(ILogger<GetNameConsumer> logger) : IConsumer<IGetName>
{
    public async Task ConsumeAsync(IConsumerContext<IGetName> context, CancellationToken token = default)
    {
        logger.LogInformation("Received: {@Context}", context);
        await Task.Delay(TimeSpan.FromSeconds(2), token); 
        // await context.ResponseAsync<INameResponse>(context.Message, token);
    }
}