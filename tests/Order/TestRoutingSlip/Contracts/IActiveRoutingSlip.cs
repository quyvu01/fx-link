namespace Order.TestRoutingSlip.Contracts;

public interface IActiveRoutingSlip
{
    string Name { get; }
    bool IsFaultSimulation { get; }
}