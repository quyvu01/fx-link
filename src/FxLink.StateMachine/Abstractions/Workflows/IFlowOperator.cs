using FxLink.Abstractions;

namespace FxLink.StateMachine.Abstractions.Workflows;

public interface IFlowOperator<TInstance, TMessage> : IFlow
    where TMessage : class where TInstance : IStateMachineInstance
{
    IEvent<TMessage> Event { get; }
    Action<TInstance, IConsumerContext<TMessage>> Action { get; }
    IFlowOperator<TInstance, TMessage> Then(Action<TInstance, IConsumerContext<TMessage>> action);
}