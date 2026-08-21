using System.Diagnostics.CodeAnalysis;
using FxLink.Exceptions;
using FxLink.Registries;

namespace FxLink.Extensions;

public static class ConsumerConfiguratorExtensions
{
    public static void UseMessageRetry<TMessage>(this IConsumerConfigurator configurator,
        [NotNull] Action<IMessageRetryPolicy> options) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(options);
        var configuratorType = configurator.GetType();
        if (!configuratorType.IsGenericType)
            throw new FxLinkException.ConsumerConfiguratorMustBeGeneric(configuratorType);
        var internalConfigurator = new ConsumerConfigurator();
        internalConfigurator.UseMessageRetry(options);
        if (configurator is not AbstractConsumerConfigurator abstractConsumerConfigurator) return;
        abstractConsumerConfigurator.AddConfigurator(typeof(TMessage), internalConfigurator.RetryPolicy);
    }
}