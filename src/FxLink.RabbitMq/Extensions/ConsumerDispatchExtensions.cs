using FxLink.Abstractions;
using FxLink.RabbitMq.Registries;
using FxLink.Registries;

namespace FxLink.RabbitMq.Extensions;

public static class ConsumerDispatchExtensions
{
    extension<TConsumer>(IConsumerConfigurator<TConsumer> configurator) where TConsumer : IConsumer
    {
        public void PrefetchCount(ushort count)
        {
            var consumerConfigurator = (ConsumerConfigurator<TConsumer>)configurator;
            var currentConfigurator = consumerConfigurator
                .GetConfigurator<IConsumerDispatchDefinition>(typeof(TConsumer));
            if (currentConfigurator is null)
            {
                var dispatchDefinition = new ConsumerDispatchDefinition();
                dispatchDefinition.SetPrefetchCount(count);
                consumerConfigurator.AddConfigurator(typeof(TConsumer), dispatchDefinition);
            }

            ((ConsumerDispatchDefinition)currentConfigurator)?.SetPrefetchCount(count);
        }

        public void ConcurrentMessageLimit(ushort limitCount)
        {
            var consumerConfigurator = (ConsumerConfigurator<TConsumer>)configurator;
            var currentConfigurator = consumerConfigurator
                .GetConfigurator<IConsumerDispatchDefinition>(typeof(TConsumer));
            if (currentConfigurator is null)
            {
                var dispatchDefinition = new ConsumerDispatchDefinition();
                dispatchDefinition.SetConcurrentMessageLimit(limitCount);
                consumerConfigurator.AddConfigurator(typeof(TConsumer), dispatchDefinition);
            }

            ((ConsumerDispatchDefinition)currentConfigurator)?.SetConcurrentMessageLimit(limitCount);
        }
    }
}
