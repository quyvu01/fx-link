using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Exceptions;

namespace FxLink.StateMachine.Registries;

internal sealed class EventConfigurator<TInstance, TMessage> :
    IEventCorrelationByConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class
{
    private Func<IConsumerContext<TMessage>, Expression<Func<TInstance, bool>>> _predicateFactory;
    private Expression<Func<IConsumerContext<TMessage>, Guid>> _correlationIdSelector;

    internal Func<IMissingInstanceConfigurator<TInstance, TMessage>, IDispatcher<IConsumerContext<TMessage>>>
        MissingInstanceBehavior { get; private set; }

    // Called at dispatch time when context is available
    internal Expression<Func<TInstance, bool>> GetPredicate(IConsumerContext<TMessage> context) =>
        _predicateFactory?.Invoke(context) ?? throw new StateMachineException.CorrelationNotConfigured();

    internal Expression<Func<IConsumerContext<TMessage>, Guid>> CorrelationSelector => _correlationIdSelector ??
        throw new StateMachineException.CorrelationNotConfigured();

    // Unlike CorrelationSelector, this does not throw: CorrelationBy() alone (matching an existing
    // instance without SelectId()) leaves no way to resolve a correlation id ahead of the DB read.
    internal Guid? GetCorrelationId(IConsumerContext<TMessage> context) =>
        _correlationIdSelector?.GetGetter().Invoke(context);

    public IEventConfigurator<TInstance, TMessage> CorrelationId(
        Expression<Func<IConsumerContext<TMessage>, Guid>> selector)
    {
        var instanceParam = Expression.Parameter(typeof(TInstance), "instance");
        var instanceCorrelationId = Expression.Property(instanceParam, nameof(IStateMachineInstance.CorrelationId));

        _predicateFactory = context =>
        {
            var contextExpr = Expression.Constant(context, typeof(IConsumerContext<TMessage>));
            var visitor = new ParameterReplacerVisitor(selector.Parameters[0], contextExpr);
            var selectorBody = visitor.Visit(selector.Body);
            var body = Expression.Equal(instanceCorrelationId, selectorBody);
            return Expression.Lambda<Func<TInstance, bool>>(body, instanceParam);
        };
        _correlationIdSelector = selector;
        return this;
    }


    public IEventCorrelationByConfigurator<TInstance, TMessage> CorrelationBy(
        Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> predicate)
    {
        _predicateFactory = context =>
        {
            var instanceParam = predicate.Parameters[0];
            var contextExpr = Expression.Constant(context, typeof(IConsumerContext<TMessage>));
            var visitor = new ParameterReplacerVisitor(predicate.Parameters[1], contextExpr);
            var newBody = visitor.Visit(predicate.Body);
            return Expression.Lambda<Func<TInstance, bool>>(newBody, instanceParam);
        };
        return this;
    }

    public IEventConfigurator<TInstance, TMessage> OnMissingInstance(
        Func<IMissingInstanceConfigurator<TInstance, TMessage>, IDispatcher<IConsumerContext<TMessage>>> missingBehavior)
    {
        MissingInstanceBehavior = missingBehavior;
        return this;
    }

    public IEventConfigurator<TInstance, TMessage>
        SelectId(Expression<Func<IConsumerContext<TMessage>, Guid>> selector)
    {
        _correlationIdSelector = selector;
        return this;
    }
}

internal sealed class ParameterReplacerVisitor(ParameterExpression target, Expression replacement) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node)
        => node == target ? replacement : base.VisitParameter(node);
}