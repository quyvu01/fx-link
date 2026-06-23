using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public sealed class EventConfigurator<TInstance, TMessage> : IEventConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    public void CorrelationId<TProp>(Expression<Func<TInstance, TProp>> selector)
    {
        throw new NotImplementedException();
    }

    public void CorrelationBy(Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> filter)
    {
        throw new NotImplementedException();
    }
}