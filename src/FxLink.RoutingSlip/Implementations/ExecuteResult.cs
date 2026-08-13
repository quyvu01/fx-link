using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Implementations;

internal class ExecuteResult(bool isCompleted) : IExecuteResult
{
    public bool IsCompleted { get; } = isCompleted;
    public Exception Exception { get; private set; }

    // No fallback placeholder: both callers (ExecuteContext<TArgument>/<TArgument,TLog>.Fault()) know
    // their own TArgument, so they always construct a RoutingSlipException.ExecuteFaultedWithoutException
    // themselves when the activity called the no-arg Fault().
    internal void Fault(Exception exception) => Exception = exception;
}

internal sealed class ExecuteResult<TLog>(bool isCompleted, TLog log) :
    ExecuteResult(isCompleted), IExecuteResult<TLog> where TLog : class
{
    public TLog Log { get; } = log;
}
internal sealed record ExecuteResultWithLog(bool IsCompleted, object Log, Exception Exception);