using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Exceptions;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.StateMachine.Implementations;

public sealed class StateMachineConsumer<TMessage>(IServiceProvider serviceProvider)
    : IConsumer<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumeContext<TMessage> context, CancellationToken token = default)
    {
        try
        {
            var consumerType = context.GetPayload<ConsumerContextWrapped>().ConsumerType;
            if (serviceProvider.GetService(consumerType) is IStateMachine stateMachine)
                await stateMachine.RaiseEventAsync(context, token);
        }
        catch (Exception e)
        {
            var requestSemantics = context.Headers.Get<string>(DistributedConfigurators.Headers.RequestSemanticsKey);
            if (requestSemantics is DistributedConfigurators.RequestSemantics.RequestAsPublisher)
            {
                if (context.RequesterId is not { } requesterId) return;
                var client = serviceProvider.GetRequiredService<IClientConnector<Result<TMessage>>>();
                await client.SendAsync(Result<TMessage>.Failed(e), new ResponseContext(context, requesterId), token);
                return;
            }

            var logger = serviceProvider.GetService<ILogger<StateMachineConsumer<TMessage>>>();
            switch (e)
            {
                case StateMachineException.InstanceMustBeInitializedFirst:
                    logger?.LogError("State machine instance must be initialized fist for context: {@Message}",
                        context);
                    return;
                case StateMachineException.EventNotDeclaredForState ex:
                    logger?.LogError(ex.Message);
                    if (context.RequesterId is not { } requesterId) return;
                    var client = serviceProvider.GetRequiredService<IClientConnector<Result<TMessage>>>();
                    await client.SendAsync(Result<TMessage>.Failed(ex), new ResponseContext(context, requesterId),
                        token);
                    return;
            }

            throw;
        }
    }
}