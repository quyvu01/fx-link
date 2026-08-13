using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Contexts;

namespace FxLink.RoutingSlip.Implementations;

internal abstract class ExecuteActivityArgProxy
{
    internal abstract Task<IExecuteResult> ExecuteAsync(object argument, IContext context,
        CancellationToken token = default);
}

internal abstract class ExecuteActivityArgLogProxy
{
    internal abstract Task<ExecuteResultWithLog> ExecuteAsync(object argument, IContext context,
        CancellationToken token = default);

    internal abstract Task<ICompensatedResult> CompensateAsync(object log, IContext context,
        CancellationToken token = default);
}

internal class ExecuteActivityProxy<TArgument>(IExecuteActivity<TArgument> activity)
    : ExecuteActivityArgProxy, IExecuteActivityProxy<TArgument> where TArgument : class
{
    public Task<IExecuteResult> ExecuteAsync(IExecuteContext<TArgument> context,
        CancellationToken token = default) => activity.ExecuteAsync(context, token);

    internal override Task<IExecuteResult> ExecuteAsync(object argument, IContext context,
        CancellationToken token = default)
    {
        var executeContext = new ExecuteContext<TArgument>((TArgument)argument, context);
        return ExecuteAsync(executeContext, token);
    }
}

internal class ExecuteActivityProxy<TArgument, TLog>(IExecuteActivity<TArgument, TLog> activity)
    : ExecuteActivityArgLogProxy, IExecuteActivityProxy<TArgument, TLog>
    where TArgument : class where TLog : class
{
    public Task<IExecuteResult<TLog>> ExecuteAsync(IExecuteContext<TArgument, TLog> context,
        CancellationToken token = default) => activity.ExecuteAsync(context, token);

    public Task<ICompensatedResult> CompensateAsync(ICompensateContext<TLog> context,
        CancellationToken token = default) => activity.CompensateAsync(context, token);

    internal override async Task<ExecuteResultWithLog> ExecuteAsync(object argument, IContext context,
        CancellationToken token = default)
    {
        var executeContext = new ExecuteContext<TArgument, TLog>((TArgument)argument, context);
        var result = await ExecuteAsync(executeContext, token);
        return new ExecuteResultWithLog(result.IsCompleted, result.Log, result.Exception);
    }

    internal override Task<ICompensatedResult> CompensateAsync(object log, IContext context,
        CancellationToken token = default)
    {
        var compensateContext = new CompensateContext<TLog>((TLog)log, context);
        return CompensateAsync(compensateContext, token);
    }
}