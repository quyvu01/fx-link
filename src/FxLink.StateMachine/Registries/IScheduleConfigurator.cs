using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Delegates;

namespace FxLink.StateMachine.Registries;

public interface IScheduleConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    TimeSpan? Delay { get; set; }
    ScheduleDelayProvider<TInstance, TMessage> DelayProvider { get; set; }
    ScheduleReceived<TInstance, TMessage> Received { get; set; }
}