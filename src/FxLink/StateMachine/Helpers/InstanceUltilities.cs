using System.Collections.Concurrent;
using System.Linq.Expressions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Exceptions;

namespace FxLink.StateMachine.Helpers;

public static class InstanceUltilities
{
    private static readonly ConcurrentDictionary<Type, Func<object>> Factories = new();

    public static TStateMachineInstance CreateInstance<TStateMachineInstance>()
        where TStateMachineInstance : IStateMachineInstance =>
        (TStateMachineInstance)Factories.GetOrAdd(typeof(TStateMachineInstance), CreateFactory).Invoke();

    private static Func<object> CreateFactory(Type instanceType)
    {
        var ctors = instanceType.GetConstructors();
        if (ctors.Length != 1 || ctors[0].GetParameters().Length != 0)
            throw new StateMachineException.InstanceMustHaveParameterlessConstructor(instanceType);

        var newExpression = Expression.New(ctors[0]);
        return Expression.Lambda<Func<object>>(newExpression).Compile();
    }
}