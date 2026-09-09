using FxLink.Abstractions;
using FxLink.Exceptions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal class BatchAccumulatorFactory<TConsumer, TMessage>(IServiceProvider serviceProvider) : IBatchAccumulatorFactory
    where TConsumer : IConsumer where TMessage : class
{
    public object CreateBatchAccumulator()
    {
        var consumerType = typeof(TConsumer);
        var resolver = serviceProvider.GetRequiredService<IConsumerConfiguratorResolver<TConsumer>>();
        var option = resolver.Resolve<IMessageBatchOption<TMessage>>(typeof(TMessage));
        if (option is not IMessageBatchOption messageBatchOption)
            throw new FxLinkException.BatchConsumerMissingBatchOptions(typeof(TMessage), consumerType);

        var configurator = messageBatchOption.GetMessageBatchConfigurator();
        var dispatcher = new BatchDispatcher<TMessage>(serviceProvider);
        return new BatchAccumulator<TMessage>(configurator,
            (messages, token) => dispatcher.DispatchAsync(messages, consumerType, token));
    }
}