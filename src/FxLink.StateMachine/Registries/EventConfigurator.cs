using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public sealed class EventConfigurator<TInstance, TMessage> :
    IEventCorrelationByConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance
    where TMessage : class
{
    private Func<IConsumerContext<TMessage>, Expression<Func<TInstance, bool>>> _predicateFactory;

    // Called at dispatch time when context is available
    private Expression<Func<TInstance, bool>> BuildPredicate(IConsumerContext<TMessage> context)
        => _predicateFactory?.Invoke(context) ?? throw new InvalidOperationException(
            "No correlation configured. Call CorrelationId or CorrelationBy first.");

    private Expression<Func<IConsumerContext<TMessage>, Guid>> _correlationIdSelector;

    public Expression<Func<TInstance, bool>> GetPredicate(IConsumerContext<TMessage> context) =>
        BuildPredicate(context);

    public Expression<Func<IConsumerContext<TMessage>, Guid>> CorrelationIdSelector() => _correlationIdSelector;

    public void CorrelationId(Expression<Func<IConsumerContext<TMessage>, Guid>> selector)
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

    public void SelectId(Expression<Func<IConsumerContext<TMessage>, Guid>> selector) =>
        _correlationIdSelector = selector;
}

internal sealed class ParameterReplacerVisitor(ParameterExpression target, Expression replacement) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node)
        => node == target ? replacement : base.VisitParameter(node);
}