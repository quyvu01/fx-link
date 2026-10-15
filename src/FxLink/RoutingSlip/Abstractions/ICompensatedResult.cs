namespace FxLink.RoutingSlip.Abstractions;

public interface ICompensatedResult
{
    bool IsCompensated { get; }
    Exception Exception { get; }
}