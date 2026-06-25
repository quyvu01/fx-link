using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IEventConfigurator;

public interface IEventConfigurator<TInstance, TMessage> : IEventConfigurator
    where TInstance : IStateMachineInstance where TMessage : class
{
    Expression<Func<TInstance, bool>> GetPredicate(IConsumerContext<TMessage> context);
    Expression<Func<IConsumerContext<TMessage>, Guid>> CorrelationIdSelector();
    void CorrelationId(Expression<Func<IConsumerContext<TMessage>, Guid>> selector);
    IEventCorrelationByConfigurator<TInstance, TMessage> CorrelationBy(Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> predicate);
}

public interface IEventCorrelationByConfigurator<TInstance, TMessage> : IEventConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    void SelectId(Expression<Func<IConsumerContext<TMessage>, Guid>> selector);
}