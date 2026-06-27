namespace FxLink.StateMachine.Abstractions.Workflows;

public interface IFlowDefinition<TInstance> : IFlow where TInstance : IStateMachineInstance
{
    IFlowOperator<TInstance, TMessage> When<TMessage>(IEvent<TMessage> @event) where TMessage : class;
}