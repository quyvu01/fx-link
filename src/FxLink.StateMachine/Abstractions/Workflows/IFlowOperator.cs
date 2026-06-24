using FxLink.Abstractions;

namespace FxLink.StateMachine.Abstractions.Workflows;

public interface IFlowOperator<out TInstance, out TMessage> : IFlow
    where TMessage : class where TInstance : IStateMachineInstance
{
    IFlowOperator<TInstance, TMessage> Then(Action<TInstance, IConsumerContext<TMessage>> action);
}