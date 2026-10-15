using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Contexts;

public interface ICompensateContext<out TLog> : IContext where TLog : class
{
    TLog Log { get; }
    ICompensatedResult Compensated();
    ICompensatedResult Fault();
    ICompensatedResult Fault(Exception exception);
}