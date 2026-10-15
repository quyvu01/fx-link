using System.Linq.Expressions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Delegates;
using FxLink.StateMachine.Exceptions;

namespace FxLink.StateMachine.Registries;

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