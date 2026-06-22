using FxLink.Core.Abstractions;
using FxLink.Core.Delegates;
using FxLink.Core.Statics;

namespace FxLink.Core.InternalPipelines;

internal class ServicesAmbientConsumerPipelineBehavior<TMessage>(IServiceProvider serviceProvider) :
    IConsumerPipelineBehavior<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        InternalServiceProvider.SetServices(serviceProvider);
        await next.Invoke(token);
    }
}