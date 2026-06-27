namespace FxLink.Abstractions;

public interface IDispatch<in TContext> where TContext : IContext
{
    Task SendAsync(TContext context, CancellationToken token = default);
}