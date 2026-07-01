using FxLink.Abstractions;
using FxLink.Delegates;
using FxLink.Statics;

namespace FxLink.InternalPipelines;

internal class ServicesAmbientConsumerPipelineBehavior<TMessage> :
    IConsumerPipelineBehavior<TMessage> where TMessage : class
{
    public ServicesAmbientConsumerPipelineBehavior(IServiceProvider serviceProvider) =>
        ServiceProviderAmbient.SetServices(serviceProvider);

    public async Task ConsumeAsync(IConsumerContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default) => await next.Invoke(token);
}