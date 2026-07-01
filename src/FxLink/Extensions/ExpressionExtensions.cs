using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace FxLink.Extensions;

public static class ExpressionExtensions
{
    private static Action<T, TProp> CreateSetter<T, TProp>(Expression<Func<T, TProp>> propertyExpression)
    {
        var param = propertyExpression.Parameters[0];
        var body = propertyExpression.Body;

        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary) body = unary.Operand;

        if (body is not MemberExpression member)
            throw new ArgumentException("Expression must be a simple property access", nameof(propertyExpression));

        var valueParam = Expression.Parameter(typeof(TProp), "value");
        var assign = Expression.Assign(member, valueParam);
        var lambda = Expression.Lambda<Action<T, TProp>>(assign, param, valueParam);

        return lambda.Compile();
    }

    private static readonly ConcurrentDictionary<Type, Delegate> SetterCache = new();

    public static Action<T, TProp> GetSetter<T, TProp>(this Expression<Func<T, TProp>> expr) =>
        (Action<T, TProp>)SetterCache.GetOrAdd(typeof(T), _ => CreateSetter(expr));
}