using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Contexts;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Delegates;

public delegate TimeSpan ScheduleDelayProvider<in TInstance>(
    IStateMachineContext<TInstance> context)
    where TInstance : IStateMachineInstance;

public delegate void ScheduleReceived<TInstance, TMessage>(
    IEventConfigurator<TInstance, TMessage> configurator)
    where TInstance : IStateMachineInstance where TMessage : class;