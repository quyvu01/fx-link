using System.Linq.Expressions;
using FxLink.Abstractions.StateMachine;
using FxLink.Delegates;
using FxLink.Exceptions;
using FxLink.Implementations.StateMachines;
using FxLink.Implementations.StateMachines.Delegates;

namespace FxLink.Registries.Implementations;

internal sealed class ScheduleConfigurator<TInstance, TMessage> :
    IScheduleConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    public TimeSpan? Delay { get; set; }
    public Expression<Func<TInstance, Guid?>> TokenIdProvider { get; set; }
    public ScheduleDelayProvider<TInstance> DelayProvider { get; set; }
    public ScheduleReceived<TInstance, TMessage> Received { get; set; }

    public void ValidateItSelf()
    {
        if (Delay is not null && DelayProvider is not null)
            throw new StateMachineException.ScheduleDelayConfiguredTwice(nameof(TMessage));
        if (Delay is null && DelayProvider is null)
            throw new StateMachineException.ScheduleDelayNotConfigured(nameof(TMessage));
        ArgumentNullException.ThrowIfNull(Received);
        ArgumentNullException.ThrowIfNull(TokenIdProvider);
    }
}
