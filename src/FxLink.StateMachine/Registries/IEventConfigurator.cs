using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IEventConfigurator<TInstance, TMessage> : IActivityConfigurator
    where TInstance : IStateMachineInstance where TMessage : class
{
    IEventConfigurator<TInstance, TMessage> CorrelationId(Expression<Func<IConsumeContext<TMessage>, Guid>> selector);

    IEventCorrelationByConfigurator<TInstance, TMessage> CorrelationBy(
        Expression<Func<TInstance, IConsumeContext<TMessage>, bool>> predicate);

    IEventConfigurator<TInstance, TMessage> OnMissingInstance(
        Func<IMissingInstanceConfigurator<TInstance, TMessage>, IDispatcher<IConsumeContext<TMessage>>> missingBehavior);
}