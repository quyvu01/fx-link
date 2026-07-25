using System.Linq.Expressions;
using FxLink.Abstractions.Contexts;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IEventCorrelationByConfigurator<TInstance, TMessage> : IEventConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    IEventConfigurator<TInstance, TMessage> SelectId(Expression<Func<IConsumerContext<TMessage>, Guid>> selector);
}