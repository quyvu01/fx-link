using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.Delegates;

namespace FxLink.Registries;

public interface IScheduleConfigurator<TInstance, TMessage> : IActivityConfigurator
    where TInstance : IStateMachineInstance where TMessage : class
{
    TimeSpan? Delay { get; set; }
    Expression<Func<TInstance, Guid?>> TokenIdProvider { get; set; }
    ScheduleDelayProvider<TInstance> DelayProvider { get; set; }
    ScheduleReceived<TInstance, TMessage> Received { get; set; }
}
