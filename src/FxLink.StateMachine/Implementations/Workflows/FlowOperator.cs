using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;

namespace FxLink.StateMachine.Implementations.Workflows;

public sealed class FlowOperator<TInstance, TMessage> : IFlowOperator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    private IEvent<TMessage> _event;
    private Action<TInstance, IConsumerContext<TMessage>> _action;
    // ctor
    public FlowOperator(IEvent<TMessage> @event) => _event = @event;
    public FlowOperator(Action<TInstance, IConsumerContext<TMessage>> action) => _action = action;

    // method (mythos :v)
    public IFlowOperator<TInstance, TMessage> Then(Action<TInstance, IConsumerContext<TMessage>> action) =>
        new FlowOperator<TInstance, TMessage>(action);
}