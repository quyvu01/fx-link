using FxLink.RoutingSlip.Contexts;

namespace FxLink.RoutingSlip.Abstractions;

internal interface IExecuteActivityProxy<in TArgument> where TArgument : class
{
    Task<IExecuteResult> ExecuteAsync(IExecuteContext<TArgument> context, CancellationToken token = default);
}

internal interface IExecuteActivityProxy<in TArgument, TLog> : IExecuteActivity where TArgument : class where TLog : class
{
    Task<IExecuteResult<TLog>> ExecuteAsync(IExecuteContext<TArgument, TLog> context,
        CancellationToken token = default);
    Task<ICompensatedResult> CompensateAsync(ICompensateContext<TLog> context, CancellationToken token = default);
}