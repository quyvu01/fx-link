using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public sealed class EventConfigurator<TInstance, TMessage> : IEventConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    private Expression _correlationIdPredicate;
    private Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> _correlationBy;
    public void CorrelationId<TProp>(Expression<Func<IConsumerContext<TMessage>, TProp>> predicate) => 
        _correlationIdPredicate = predicate;

    public void CorrelationBy(Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> filter) =>
        _correlationBy = filter;
}