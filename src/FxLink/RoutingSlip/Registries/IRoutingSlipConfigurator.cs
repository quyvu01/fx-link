using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Registries;

public interface IRoutingSlipConfigurator
{
    IRoutingSlipConfigurator AddActivity<TActivity>() where TActivity : IExecuteActivity;

    // Also reachable by Uri, alongside its normal typed-argument path — an orchestrator that only
    // knows the destination string (not this activity's TArguments assembly) can still target it
    // via RoutingSlipBuilderExtensions.AddArgument(Uri, object).
    IRoutingSlipConfigurator AddActivity<TActivity>(Uri uri) where TActivity : IExecuteActivity;
}
