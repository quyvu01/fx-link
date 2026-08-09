using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IEventConfigurator<TInstance, TMessage> : IActivityConfigurator
    where TInstance : IStateMachineInstance where TMessage : class
{
    IEventConfigurator<TInstance, TMessage> CorrelationId(Expression<Func<IConsumerContext<TMessage>, Guid>> selector);

    IEventCorrelationByConfigurator<TInstance, TMessage> CorrelationBy(
        Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> predicate);

    IEventConfigurator<TInstance, TMessage> OnMissingInstance(
        Func<IMissingInstanceConfigurator<TInstance, TMessage>, IDispatcher<IConsumerContext<TMessage>>> missingBehavior);
}