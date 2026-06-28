namespace FxLink.Abstractions;

public interface IDispatcher<in TContext> where TContext : IContext
{
    Task SendAsync(TContext context, CancellationToken token = default);
}