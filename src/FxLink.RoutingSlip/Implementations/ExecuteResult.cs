using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Implementations;

internal class ExecuteResult(bool isCompleted) : IExecuteResult
{
    public bool IsCompleted { get; } = isCompleted;
    public Exception Exception { get; private set; }

    internal void Fault(Exception exception = null) =>
        Exception = exception ?? new Exception(); // Todo: Update Exception type to match with exception...
}

internal sealed class ExecuteResult<TLog>(bool isCompleted, TLog log) :
    ExecuteResult(isCompleted), IExecuteResult<TLog> where TLog : class
{
    public TLog Log { get; } = log;
}
internal sealed record ExecuteResultWithLog(bool IsCompleted, object Log, Exception Exception);