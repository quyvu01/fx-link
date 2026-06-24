using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IEventConfigurator;

public interface IEventConfigurator<TInstance, TMessage> : IEventConfigurator
    where TInstance : IStateMachineInstance where TMessage : class
{
    void CorrelationId<TProp>(Expression<Func<IConsumerContext<TMessage>, TProp>> predicate);
    void CorrelationBy(Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> filter);
}