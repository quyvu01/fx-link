using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Delegates;

public delegate TimeSpan ScheduleDelayProvider<in TInstance, in TMessage>(
    IStateMachineContext<TInstance, TMessage> context)
    where TInstance : IStateMachineInstance where TMessage : class;

public delegate void ScheduleReceived<TInstance, TMessage>(
    IEventConfigurator<TInstance, TMessage> configurator)
    where TInstance : IStateMachineInstance where TMessage : class;