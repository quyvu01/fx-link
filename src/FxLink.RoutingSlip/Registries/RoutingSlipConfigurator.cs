using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Entities;
using FxLink.RoutingSlip.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.RoutingSlip.Registries;

internal sealed class RoutingSlipConfigurator(IServiceCollection services) : IRoutingSlipConfigurator
{
    internal IReadOnlyDictionary<Type, List<Type>> MessageKeys => _messageKeys;
    private readonly ConcurrentDictionary<Type, List<Type>> _messageKeys = [];
    private readonly HashSet<ActivityData> _activitiesData = [];

    public IRoutingSlipConfigurator AddActivity<TActivity>() where TActivity : IExecuteActivity =>
        AddActivityInternal(typeof(TActivity));

    private IRoutingSlipConfigurator AddActivityInternal(Type activityType)
    {
        foreach (var @interface in activityType.GetInterfaces())
        {
            if (!@interface.IsGenericType) continue;
            var typeDefinition = @interface.GetGenericTypeDefinition();
            var args = @interface.GetGenericArguments();
            if (typeDefinition == typeof(IExecuteActivity<>))
            {
                _activitiesData.Add(new ActivityData(activityType, args.First()));
                continue;
            }

            if (typeDefinition == typeof(IExecuteActivity<,>))
                _activitiesData.Add(new ActivityData(activityType, args.First(), args.Last()));
        }

        return this;
    }

    internal void Build()
    {
        foreach (var activityData in _activitiesData)
        {
            RegisterConsumer(activityData);

            if (activityData.LogsType is null)
            {
                var singleArgsServiceType = typeof(IExecuteActivity<>)
                    .MakeGenericType(activityData.ArgumentsType);
                services.TryAddEnumerable(new ServiceDescriptor(singleArgsServiceType, activityData.ActivityType,
                    ServiceLifetime.Scoped));
                continue;
            }

            var fullArgsServiceType = typeof(IExecuteActivity<,>)
                .MakeGenericType(activityData.ArgumentsType, activityData.LogsType);
            services.TryAddEnumerable(new ServiceDescriptor(fullArgsServiceType, activityData.ActivityType,
                ServiceLifetime.Scoped));
        }
    }

    private void RegisterConsumer(ActivityData activityData)
    {
        var (activityType, argumentsType, logsType) = activityData;
        RegisterConsumer(activityType, argumentsType);
        if (logsType is not null) RegisterConsumer(activityType, typeof(ActivityLogEntry));
    }

    private void RegisterConsumer(Type serviceKey, Type messageType)
    {
        var serviceType = typeof(IConsumer<>).MakeGenericType(messageType);
        var implementType = typeof(RoutingSlipConsumer<>).MakeGenericType(messageType);
        services.TryAddEnumerable(new ServiceDescriptor(serviceType, serviceKey,
            implementationType: implementType, ServiceLifetime.Scoped));
        var keys = _messageKeys.GetOrAdd(messageType, _ => []);
        if (!keys.Contains(serviceKey)) keys.Add(serviceKey);
    }

    private record ActivityData(Type ActivityType, Type ArgumentsType, Type LogsType = null);
}