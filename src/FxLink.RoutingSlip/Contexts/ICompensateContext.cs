using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Contexts;

public interface ICompensateContext<out TLogs> : IContext where TLogs : class
{
    TLogs Logs { get; }
    ICompensateResult Compensated();
    ICompensateResult Fault();
    ICompensateResult Fault(Exception exception);
}