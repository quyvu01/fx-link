using System.Linq.Expressions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Delegates;
using FxLink.StateMachine.Exceptions;

namespace FxLink.StateMachine.Registries;

public sealed class ScheduleConfigurator<TInstance, TMessage> :
    IScheduleConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    public TimeSpan? Delay { get; set; }
    public Expression<Func<TInstance, Guid?>> TokenIdProvider { get; set; }
    public ScheduleDelayProvider<TInstance> DelayProvider { get; set; }
    public ScheduleReceived<TInstance, TMessage> Received { get; set; }

    public void Validate()
    {
        if (Delay is not null && DelayProvider is not null)
            throw new StateMachineException.ScheduleTimeCannotBeRegisteredBothDelayAndDelayProvider(nameof(TMessage));
        if (Delay is null && DelayProvider is null)
            throw new StateMachineException.ScheduleTimeMustBeRegister(nameof(TMessage));
        ArgumentNullException.ThrowIfNull(Received);
        ArgumentNullException.ThrowIfNull(TokenIdProvider);
    }
}