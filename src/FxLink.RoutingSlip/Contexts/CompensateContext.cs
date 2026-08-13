using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Exceptions;
using FxLink.RoutingSlip.Implementations;

namespace FxLink.RoutingSlip.Contexts;

internal sealed class CompensateContext<TLog>
    : AbstractContext, ICompensateContext<TLog> where TLog : class
{
    public CompensateContext(TLog log, Guid correlationId, IHeaders headers)
        : base(correlationId, headers) => Log = log;

    public CompensateContext(TLog log, IContext context) : base(context.CorrelationId, context.Headers)
        => Log = log;

    public TLog Log { get; }

    public ICompensatedResult Compensated() => new CompensatedResult(true, null);

    public ICompensatedResult Fault() => Fault(new RoutingSlipException.CompensateFaultedWithoutException(typeof(TLog)));
    public ICompensatedResult Fault(Exception exception) => new CompensatedResult(false, exception);
}