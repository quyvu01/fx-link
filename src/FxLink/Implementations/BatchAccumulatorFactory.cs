using System.Reflection;
using FxLink.Abstractions;
using FxLink.Exceptions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal static class BatchAccumulatorFactory
{
    private static readonly MethodInfo CreateTypedMethod = typeof(BatchAccumulatorFactory)
        .GetMethod(nameof(CreateTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    internal static object Create(Type consumerType, Type messageType, IServiceProvider serviceProvider) =>
        CreateTypedMethod.MakeGenericMethod(consumerType, messageType).Invoke(null, [serviceProvider]);

    private static BatchAccumulator<TMessage> CreateTyped<TConsumer, TMessage>(IServiceProvider serviceProvider)
        where TConsumer : IConsumer where TMessage : class
    {
        var consumerType = typeof(TConsumer);
        var resolver = serviceProvider.GetRequiredService<IConsumerConfiguratorResolver<TConsumer>>();
        var option = resolver.Resolve<IMessageBatchOption<TMessage>>(typeof(TMessage))
                     ?? throw new FxLinkException.BatchConsumerMissingBatchOptions(typeof(TMessage), consumerType);

        var configurator = ((MessageBatchOption)option).GetMessageBatchConfigurator();
        var dispatcher = new BatchDispatcher<TMessage>(serviceProvider);
        return new BatchAccumulator<TMessage>(configurator,
            (messages, token) => dispatcher.DispatchAsync(messages, consumerType, token));
    }
}