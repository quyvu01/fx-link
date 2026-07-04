using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Delegates;

namespace FxLink.Implementations;

public class Dispatcher<TContext>(DispatchHandlerDelegate dispatchHandlerDelegate)
    : IDispatcher<TContext> where TContext : IContext
{
    public async Task SendAsync(TContext context, CancellationToken token = default)
    {
        if (dispatchHandlerDelegate is not null)
            await dispatchHandlerDelegate.Invoke(context, token);
    }
}