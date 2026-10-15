using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Registries;

namespace FxLink.Delegates;

public delegate TimeSpan ScheduleDelayProvider<in TInstance>(
    IStateMachineContext<TInstance> context)
    where TInstance : IStateMachineInstance;

public delegate void ScheduleReceived<TInstance, TMessage>(
    IEventConfigurator<TInstance, TMessage> configurator)
    where TInstance : IStateMachineInstance where TMessage : class;
