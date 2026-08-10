using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Registries;

public interface IRoutingSlipConfigurator
{
    IRoutingSlipConfigurator AddActivity<TActivity>() where TActivity : IExecuteActivity;
}