using FxLink.Abstractions;
using FxLink.Delegates;

namespace FxLink.Implementations;

public class Dispatch<TContext>(DispatchHandlerDelegate dispatchHandlerDelegate)
    : IDispatch<TContext> where TContext : IContext
{
    public async Task SendAsync(TContext context, CancellationToken token = default)
    {
        if (dispatchHandlerDelegate is not null)
            await dispatchHandlerDelegate.Invoke(context, token);
    }
}