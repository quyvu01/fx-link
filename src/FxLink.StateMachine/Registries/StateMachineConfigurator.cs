using System.Collections.Concurrent;
using System.Reflection;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.Faults;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Registries;

public sealed class StateMachineConfigurator(IServiceCollection services) : IStateMachineConfigurator
{
    public IReadOnlyDictionary<Type, List<object>> MessageKeys => _messageKeys;
    private readonly ConcurrentDictionary<Type, List<object>> _messageKeys = [];

    public IStateMachineConfigurator Of<TStateMachine>(Action<IStateMachineSetup> config = null)
        where TStateMachine : IStateMachine
    {
        services.AddSingleton(typeof(TStateMachine));
        // Find all event then register consumer, seems we need something to map or delegate, maybe
        RegisterStateMachineConsumers<TStateMachine>();
        var stateMachineSetup = new StateMachineSetup(services);
        config?.Invoke(stateMachineSetup);
        return this;
    }

    private void RegisterStateMachineConsumers<TStateMachine>()
        where TStateMachine : IStateMachine
    {
        var serviceKey = typeof(TStateMachine);
        typeof(TStateMachine)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(t => typeof(IActivity).IsAssignableFrom(t.PropertyType) && t.PropertyType.IsInterface)
            .Where(t => t.PropertyType.IsGenericType)
            .ForEach(p =>
            {
                var eventType = p.PropertyType;
                var genericTypeDefinition = p.PropertyType.GetGenericTypeDefinition();
                try
                {
                    if (genericTypeDefinition == typeof(IEvent<>) || genericTypeDefinition == typeof(ISchedule<>))
                    {
                        var arg = eventType.GetGenericArguments()[0];
                        RegisterConsumer(arg);
                        return;
                    }

                    if (genericTypeDefinition == typeof(IRequest<,>))
                    {
                        var args = eventType.GetGenericArguments();
                        var arg0 = args[0]; // failed or timeout
                        var arg1 = args[1]; // succeed
                        RegisterConsumer(typeof(Fault<>).MakeGenericType(arg0));
                        RegisterConsumer(typeof(RequestTimeoutExpired<>).MakeGenericType(arg0));
                        RegisterConsumer(arg1);
                    }
                }
                catch (Exception)
                {
                    throw new StateMachineException.EventIsNotCorrect(eventType);
                }
            });
        return;

        void RegisterConsumer(Type arg)
        {
            var serviceType = typeof(IConsumer<>).MakeGenericType(arg);
            var implementType = typeof(StateMachineConsumer<>).MakeGenericType(arg);
            services.Add(new ServiceDescriptor(serviceType, serviceKey: typeof(TStateMachine),
                implementationType: implementType, ServiceLifetime.Scoped));
            var keys = _messageKeys.GetOrAdd(arg, _ => []);
            keys.Add(serviceKey);
        }
    }
}