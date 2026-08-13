using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Contexts;

public interface IExecuteContext
{
    IExecuteResult Fault(Exception exception);
    IExecuteResult Fault();
}

public interface IExecuteContext<out TArgument> : IExecuteContext, IContext where TArgument : class
{
    TArgument Argument { get; }
    IExecuteResult Completed();
}

public interface IExecuteContext<out TArgument, TLog> : IExecuteContext, IContext
    where TArgument : class where TLog : class
{
    TArgument Argument { get; }
    TLog Log { get; }
    IExecuteResult<TLog> Completed(TLog logs);
}