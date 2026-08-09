using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Contexts;

public interface IExecuteContext<out TArguments> : IContext where TArguments : class
{
    TArguments Arguments { get; }
    IExecuteResult Completed<TLogs>(TLogs logs) where TLogs : class;
    IExecuteResult Completed();
    IExecuteResult Fault<TException>(TException exception) where TException : Exception;
    IExecuteResult Fault();
}