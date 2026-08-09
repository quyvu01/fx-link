using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Abstractions;

public interface IStateMachine : IConsumer
{
    IState Initial { get; }
    IState Completed { get; }
    IState[] States { get; }
    IReadOnlyDictionary<IActivity, IActivityConfigurator> InternalActivityConfigurators { get; }

    Task RaiseEventAsync<TMessage>(IConsumerContext<TMessage> context, CancellationToken token = default)
        where TMessage : class;
}