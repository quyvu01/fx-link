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

    internal override async Task<IExecuteResult> ExecuteAsync(object argument, IContext context,
        CancellationToken token = default)
    {
        try
        {
            var executeContext = new ExecuteContext<TArgument>((TArgument)argument, context);
            return await ExecuteAsync(executeContext, token);
        }
        catch (Exception e)
        {
            var result = new ExecuteResult(false);
            result.Fault(e);
            return result;
        }
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
        try
        {
            var executeContext = new ExecuteContext<TArgument, TLog>((TArgument)argument, context);
            var result = await ExecuteAsync(executeContext, token);
            return new ExecuteResultWithLog(result.IsCompleted, result.Log, result.Exception);
        }
        catch (Exception e)
        {
            return new ExecuteResultWithLog(false, null, e);
        }
    }

    internal override async Task<ICompensatedResult> CompensateAsync(object log, IContext context,
        CancellationToken token = default)
    {
        try
        {
            var compensateContext = new CompensateContext<TLog>((TLog)log, context);
            return await CompensateAsync(compensateContext, token);
        }
        catch (Exception e)
        {
            return new CompensatedResult(false, e);
        }
    }
}