using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Contexts;

public interface ICompensateContext<out TLog> : IContext where TLog : class
{
    TLog Log { get; }
    ICompensateResult Compensated();
    ICompensateResult Fault();
    ICompensateResult Fault(Exception exception);
}