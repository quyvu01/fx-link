using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IEventConfigurator<TInstance, TMessage> where TInstance : IStateMachineInstance where TMessage : class
{
    void CorrelationId<TProp>(Expression<Func<TInstance, TProp>> selector);
    void CorrelationBy(Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> filter);
}