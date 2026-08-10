using FxLink.Abstractions;
using FxLink.RoutingSlip.Contexts;

namespace FxLink.RoutingSlip.Abstractions;

public interface IExecuteActivity : IConsumer;

public interface IExecuteActivity<in TArguments> : IExecuteActivity where TArguments : class
{
    Task<IExecuteResult> ExecuteAsync(IExecuteContext<TArguments> context, CancellationToken token = default);
}

public interface IExecuteActivity<in TArguments, in TLogs> : IExecuteActivity<TArguments>
    where TArguments : class where TLogs : class
{
    Task<ICompensateResult> CompensateAsync(ICompensateContext<TLogs> context, CancellationToken token = default);
}