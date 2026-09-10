using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Contexts;

public interface IExecuteContext<out TArgument> : IContext where TArgument : class
{
    IExecuteResult Fault(Exception exception = null);
    TArgument Argument { get; }
    IExecuteResult Completed();
}

public interface IExecuteContext<out TArgument, TLog> : IContext
    where TArgument : class where TLog : class
{
    TArgument Argument { get; }
    TLog Log { get; }
    IExecuteResult<TLog> Completed(TLog logs);
    IExecuteResult<TLog> Fault(Exception exception = null);
}