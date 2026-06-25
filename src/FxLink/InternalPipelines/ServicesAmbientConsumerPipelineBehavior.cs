using FxLink.Abstractions;
using FxLink.Delegates;
using FxLink.Statics;

namespace FxLink.InternalPipelines;

internal class ServicesAmbientConsumerPipelineBehavior<TMessage>(IServiceProvider serviceProvider) :
    IConsumerPipelineBehavior<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        ServiceProviderAmbient.SetServices(serviceProvider);
        await next.Invoke(token);
    }
}