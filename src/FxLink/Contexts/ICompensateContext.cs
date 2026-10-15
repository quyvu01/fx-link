using FxLink.Contexts;
using FxLink.Abstractions;

namespace FxLink.Contexts;

public interface ICompensateContext<out TLog> : IContext where TLog : class
{
    TLog Log { get; }
    ICompensatedResult Compensated();
    ICompensatedResult Fault();
    ICompensatedResult Fault(Exception exception);
}
