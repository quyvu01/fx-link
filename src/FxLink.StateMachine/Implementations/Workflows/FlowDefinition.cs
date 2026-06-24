using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;

namespace FxLink.StateMachine.Implementations.Workflows;

public sealed class FlowDefinition<TInstance>  : IFlowDefinition<TInstance> where TInstance : IStateMachineInstance
{
    public IFlowOperator<TInstance, TMessage> On<TMessage>(IEvent<TMessage> @event) where TMessage : class
    {
        throw new NotImplementedException();
    }
}