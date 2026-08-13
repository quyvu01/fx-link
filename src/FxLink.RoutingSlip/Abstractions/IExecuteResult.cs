namespace FxLink.RoutingSlip.Abstractions;

public interface IExecuteResult
{
    bool IsCompleted { get; }
    Exception Exception { get; }
}

public interface IExecuteResult<out TLog> : IExecuteResult where TLog : class
{
    TLog Log { get; }
}