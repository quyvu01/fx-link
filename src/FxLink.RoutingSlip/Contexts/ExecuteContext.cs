using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Implementations;

namespace FxLink.RoutingSlip.Contexts;

internal class ExecuteContext<TArgument> : AbstractContext, IExecuteContext<TArgument> where TArgument : class
{
    public ExecuteContext(TArgument argument, Guid correlationId, IHeaders headers)
        : base(correlationId, headers) => Argument = argument;

    public ExecuteContext(TArgument argument, IContext context) : base(context.CorrelationId, context.Headers)
        => Argument = argument;

    public IExecuteResult Fault(Exception exception)
    {
        var executionResult = new ExecuteResult(false);
        executionResult.Fault(exception);
        return executionResult;
    }

    public IExecuteResult Fault()
    {
        var executionResult = new ExecuteResult(false);
        executionResult.Fault();
        return executionResult;
    }

    public TArgument Argument { get; }

    public IExecuteResult Completed() => new ExecuteResult(true);
}

internal sealed class ExecuteContext<TArgument, TLog> : AbstractContext, IExecuteContext<TArgument, TLog>
    where TArgument : class where TLog : class
{
    public ExecuteContext(TArgument argument, Guid correlationId, IHeaders headers)
        : base(correlationId, headers) => Argument = argument;

    public ExecuteContext(TArgument argument, IContext context) : base(context.CorrelationId, context.Headers)
        => Argument = argument;

    public IExecuteResult Fault(Exception exception)
    {
        var executionResult = new ExecuteResult(false);
        executionResult.Fault(exception);
        return executionResult;
    }

    public IExecuteResult Fault()
    {
        var executionResult = new ExecuteResult(false);
        executionResult.Fault();
        return executionResult;
    }

    public TArgument Argument { get; }
    public TLog Log { get; private set; }

    public IExecuteResult<TLog> Completed(TLog logs) => new ExecuteResult<TLog>(true, logs);
}