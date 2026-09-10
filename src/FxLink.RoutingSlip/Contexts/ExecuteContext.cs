using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Exceptions;
using FxLink.RoutingSlip.Implementations;

namespace FxLink.RoutingSlip.Contexts;

internal class ExecuteContext<TArgument> : AbstractContext, IExecuteContext<TArgument> where TArgument : class
{
    public ExecuteContext(TArgument argument, IHeaders headers, Guid correlationId)
        : base(headers, correlationId) => Argument = argument;

    public ExecuteContext(TArgument argument, IContext context) : base(context.Headers, context.CorrelationId)
        => Argument = argument;

    public IExecuteResult Fault(Exception exception = null)
    {
        var executionResult = new ExecuteResult(false);
        var finalException = exception ?? new RoutingSlipException.ExecuteFaultedWithoutException(typeof(TArgument));
        executionResult.Fault(finalException);
        return executionResult;
    }

    public TArgument Argument { get; }

    public IExecuteResult Completed() => new ExecuteResult(true);
}

internal sealed class ExecuteContext<TArgument, TLog> : AbstractContext, IExecuteContext<TArgument, TLog>
    where TArgument : class where TLog : class
{
    public ExecuteContext(TArgument argument, IHeaders headers, Guid correlationId)
        : base(headers, correlationId) => Argument = argument;

    public ExecuteContext(TArgument argument, IContext context) : base(context.Headers, context.CorrelationId)
        => Argument = argument;

    public IExecuteResult<TLog> Fault(Exception exception = null)
    {
        var executionResult = new ExecuteResult<TLog>(false, null);
        var finalException = exception ?? new RoutingSlipException.ExecuteFaultedWithoutException(typeof(TArgument));
        executionResult.Fault(finalException);
        return executionResult;
    }

    public TArgument Argument { get; }
    public TLog Log { get; private set; }

    public IExecuteResult<TLog> Completed(TLog logs) => new ExecuteResult<TLog>(true, logs);
}