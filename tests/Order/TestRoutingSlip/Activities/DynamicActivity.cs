using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Contexts;
using Order.TestRoutingSlip.Arguments;

namespace Order.TestRoutingSlip.Activities;

public sealed class DynamicActivity(ILogger<DynamicActivity> logger) : IExecuteActivity<DynamicArgs>
{
    public async Task<IExecuteResult> ExecuteAsync(IExecuteContext<DynamicArgs> context,
        CancellationToken token = default)
    {
        logger.LogInformation("Start consuming message for [DynamicArgs]: {@Args}", context.Argument);
        await Task.Delay(TimeSpan.FromSeconds(3), token);
        return context.Completed();
    }
}