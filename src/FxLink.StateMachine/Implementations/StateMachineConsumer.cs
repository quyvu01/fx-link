using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.StateMachine.Implementations;

public sealed class StateMachineConsumer<TMessage>(
    ILogger<StateMachineConsumer<TMessage>> logger,
    IMessageKeys messageKeys,
    IServiceProvider serviceProvider) : IConsumer<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default)
    {
        logger.LogInformation("State machine consumers: {@Message}", context.Message);
        var stateMachineTypes = messageKeys.GetMessageKeys(typeof(TMessage))
            .OfType<Type>()
            .Where(t => typeof(IStateMachine).IsAssignableFrom(t))
            .ToArray();
        var tasks = stateMachineTypes.Select(async stateMachineType =>
        {
            using var scope = serviceProvider.CreateScope();
            var stateMachine = (scope.ServiceProvider.GetService(stateMachineType) as IStateMachine)!;
            await stateMachine.RaiseEventAsync(context.Message,
                new RequestContext(context.CorrelationId, context.Headers), token);
        });
        await Task.WhenAll(tasks);
    }
}