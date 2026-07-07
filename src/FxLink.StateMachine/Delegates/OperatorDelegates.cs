using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Delegates;

public delegate Task AsyncOperatorAction<in TInstance, in TMessage>(IStateMachineContext<TInstance, TMessage> context,
    CancellationToken token = default) where TInstance : IStateMachineInstance where TMessage : class;

public delegate void OperatorAction<in TInstance, in TMessage>(IStateMachineContext<TInstance, TMessage> context)
    where TInstance : IStateMachineInstance where TMessage : class;

public delegate Task<bool> AsyncOperatorCondition<in TInstance, in TMessage>(
    IStateMachineContext<TInstance, TMessage> context,
    CancellationToken token = default) where TInstance : IStateMachineInstance where TMessage : class;

public delegate bool OperatorCondition<in TInstance, in TMessage>(
    IStateMachineContext<TInstance, TMessage> context) where TInstance : IStateMachineInstance where TMessage : class;

public delegate IFlowOperator<TInstance, TMessage> ActivityOperatorCallback<TInstance, TMessage>(
    IFlowOperator<TInstance, TMessage> @operator) where TInstance : IStateMachineInstance where TMessage : class;

public delegate T MessageOperatorFactory<in TInstance, in TMessage, out T>(
    IStateMachineContext<TInstance, TMessage> context)
    where TInstance : IStateMachineInstance where TMessage : class where T : class;

public delegate Task<T> MessageOperatorFactoryAsync<in TInstance, in TMessage, T>(
    IStateMachineContext<TInstance, TMessage> context, CancellationToken token = default)
    where TInstance : IStateMachineInstance where TMessage : class where T : class;

public delegate void StateMchineOperatorActivity<in TInstance, in TMessage>(
    IStateMachineActivityConfigurator<TInstance, TMessage> options)
    where TInstance : IStateMachineInstance where TMessage : class;