namespace FxLink.Abstractions.RoutingSlip;

public interface IRoutingSlipBuilder
{
    IRoutingSlipBuilder AddArgument<TArguments>(TArguments arguments);
    IRoutingSlipBuilder SetVariable(string key, object value);
}
