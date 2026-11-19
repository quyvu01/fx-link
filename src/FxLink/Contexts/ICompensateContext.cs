using FxLink.Abstractions;
using FxLink.Abstractions.RoutingSlip;

namespace FxLink.Contexts;

public interface ICompensateContext<out TLog> : IContext where TLog : class
{
    TLog Log { get; }
    ICompensatedResult Compensated();
    ICompensatedResult Fault();
    ICompensatedResult Fault(Exception exception);
}
