using FxLink.Abstractions.RoutingSlip;

namespace FxLink.Implementations;

internal sealed class CompensatedResult(bool isCompensated, Exception exception) : ICompensatedResult
{
    public bool IsCompensated { get; } = isCompensated;
    public Exception Exception { get; } = exception;
}
