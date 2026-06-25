using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Implementations;

internal class StateMachineInstanceInMemory : IStateMachineInstancePersistence
{
    private readonly ConcurrentBag<object> _instances = [];

    public Task<TInstance> GetInstance<TInstance>(Expression<Func<TInstance, bool>> filter)
        where TInstance : IStateMachineInstance
    {
        var instance = _instances.OfType<TInstance>()
            .Where(filter.Compile())
            .FirstOrDefault();
        return Task.FromResult(instance);
    }

    // Todo: Check the best way to init a new StateMachine instance. Or we can put a validation like new().
    // Also, we have to check the Initial state, this is the very simple example to set the state(to test only)
    // We need to have more scenario for state setting like enum, int, string or immutable object?
    public Task<TInstance> CreateInstanceAsync<TInstance>(Guid correlationId, Expression<Func<TInstance, object>> stateSelector)
        where TInstance : IStateMachineInstance
    {
        var newInstance = (TInstance)Activator.CreateInstance(typeof(TInstance))!;
        _instances.Add(newInstance);
        newInstance.CorrelationId = correlationId;

        var property = ExtractProperty(stateSelector);
        property.SetValue(newInstance, "Initial");

        return Task.FromResult(newInstance);
    }

    private static PropertyInfo ExtractProperty<TInstance>(Expression<Func<TInstance, object>> selector)
    {
        // Handle boxing: (instance) => (object)instance.State
        var body = selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary
            ? unary.Operand
            : selector.Body;

        if (body is MemberExpression { Member: PropertyInfo property })
            return property;

        throw new ArgumentException(
            $"Selector must be a simple property access, e.g. x => x.State. Got: {selector.Body}");
    }
}