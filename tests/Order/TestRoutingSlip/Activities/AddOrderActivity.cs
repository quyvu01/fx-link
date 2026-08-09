using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Contexts;
using Order.TestRoutingSlip.Arguments;
using Order.TestRoutingSlip.RoutingSlipLogs;

namespace Order.TestRoutingSlip.Activities;

public sealed class AddOrderActivity(ILogger<AddOrderActivity> logger) : IExecuteActivity<AddOrderArgs, AddOrderLogs>
{
    public async Task<IExecuteResult> ExecuteAsync(IExecuteContext<AddOrderArgs> context,
        CancellationToken token = default)
    {
        logger.LogInformation("Received args: {@Args}", context.Arguments);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        return context.Completed(new AddOrderLogs { Name = context.Arguments.Name, ActionTime = DateTime.UtcNow });
    }

    public async Task<ICompensateResult> CompensateAsync(ICompensateContext<AddOrderLogs> context,
        CancellationToken token = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        logger.LogInformation("Before compensated: {@Logs}", context.Logs);
        return context.Compensated();
    }
}