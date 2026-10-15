using FxLink.Contexts;
using FxLink.Abstractions;
using FxLink.Exceptions;
using FxLink.Implementations;

namespace FxLink.Contexts;

internal sealed class CompensateContext<TLog>
    : AbstractContext, ICompensateContext<TLog> where TLog : class
{
    public CompensateContext(TLog log, IHeaders headers, Guid correlationId)
        : base(headers, correlationId) => Log = log;

    public CompensateContext(TLog log, IContext context) : base(context.Headers, context.CorrelationId)
        => Log = log;

    public TLog Log { get; }

    public ICompensatedResult Compensated() => new CompensatedResult(true, null);

    public ICompensatedResult Fault() => Fault(new RoutingSlipException.CompensateFaultedWithoutException(typeof(TLog)));
    public ICompensatedResult Fault(Exception exception) => new CompensatedResult(false, exception);
}
