using FxLink.Abstractions;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Abstractions;

public interface IStateMachine
{
    IState[] States { get; }
    IReadOnlyDictionary<IActivity, IActivityConfigurator> ActivityConfigurators { get; }
    Task RaiseEventAsync<TMessage>(IConsumerContext<TMessage> context, CancellationToken token = default)
        where TMessage : class;
}