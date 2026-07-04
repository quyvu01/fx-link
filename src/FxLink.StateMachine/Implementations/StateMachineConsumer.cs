using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.StateMachine.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Implementations;

public sealed class StateMachineConsumer<TMessage>(IMessageKeys messageKeys, IServiceProvider serviceProvider)
    : IConsumer<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default)
    {
        var stateMachineTypes = messageKeys.GetMessageKeys(typeof(TMessage))
            .OfType<Type>()
            .Where(t => typeof(IStateMachine).IsAssignableFrom(t));

        var tasks = stateMachineTypes.Select(async stateMachineType =>
        {
            using var scope = serviceProvider.CreateScope();
            var stateMachine = (scope.ServiceProvider.GetService(stateMachineType) as IStateMachine)!;
            await stateMachine.RaiseEventAsync(context, token);
        });
        await Task.WhenAll(tasks);
    }
}