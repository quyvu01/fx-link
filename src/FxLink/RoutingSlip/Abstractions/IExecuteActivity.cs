using FxLink.Abstractions;
using FxLink.RoutingSlip.Contexts;

namespace FxLink.RoutingSlip.Abstractions;

public interface IExecuteActivity : IConsumer;

public interface IExecuteActivity<in TArgument> : IExecuteActivity where TArgument : class
{
    Task<IExecuteResult> ExecuteAsync(IExecuteContext<TArgument> context, CancellationToken token = default);
}

public interface IExecuteActivity<in TArgument, TLog> : IExecuteActivity
    where TArgument : class where TLog : class
{
    Task<IExecuteResult<TLog>> ExecuteAsync(IExecuteContext<TArgument, TLog> context, CancellationToken token = default);
    Task<ICompensatedResult> CompensateAsync(ICompensateContext<TLog> context, CancellationToken token = default);
}