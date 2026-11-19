namespace FxLink.Abstractions.RoutingSlip;

public interface ICompensatedResult
{
    bool IsCompensated { get; }
    Exception Exception { get; }
}
