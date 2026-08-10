namespace FxLink.RoutingSlip.Abstractions;

public interface IRoutingSlipBuilder
{
    IRoutingSlipBuilder AddArgument<TArguments>(TArguments arguments);
    IRoutingSlipBuilder SetVariable(string key, object value);
}