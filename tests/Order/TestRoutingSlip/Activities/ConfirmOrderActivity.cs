using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Contexts;
using Order.TestRoutingSlip.Arguments;

namespace Order.TestRoutingSlip.Activities;

public sealed class ConfirmOrderActivity(ILogger<ConfirmOrderActivity> logger) : IExecuteActivity<ConfirmOrderArgs>
{
    public async Task<IExecuteResult> ExecuteAsync(IExecuteContext<ConfirmOrderArgs> context,
        CancellationToken token = default)
    {
        logger.LogInformation("[ConfirmOrderActivity] with args: {@Args}", context.Argument);
        await Task.Delay(TimeSpan.FromSeconds(5), token);
        return context.Argument.IsFaultSimulation ? context.Fault() : context.Completed();
    }
}