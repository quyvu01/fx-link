namespace FxLink.StateMachine.Abstractions.Workflows;

public interface IEventDefinition<TInstance> : IEventOperator where TInstance : IStateMachineInstance
{
    IEventOperator<TInstance, TMessage> When<TMessage>(IEvent<TMessage> @event) where TMessage : class;
}
