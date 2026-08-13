using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Contexts;
using Order.TestRoutingSlip.Arguments;

namespace Order.TestRoutingSlip.Activities;

// No TLog: a sent notification isn't worth undoing, so this step never contributes an ActivityLog
// entry and is automatically skipped by the compensate backward-walk. Terminal step of the sample
// saga — only reached when ReserveInventory/AddOrder/ChargeOrderPayment/ConfirmOrder all succeeded.
public sealed class NotifyCustomerActivity(ILogger<NotifyCustomerActivity> logger)
    : IExecuteActivity<NotifyCustomerArgs>
{
    public async Task<IExecuteResult> ExecuteAsync(IExecuteContext<NotifyCustomerArgs> context,
        CancellationToken token = default)
    {
        logger.LogInformation("[NotifyCustomerActivity] notifying: {@Args}", context.Argument);
        await Task.Delay(TimeSpan.FromMilliseconds(500), token);
        return context.Completed();
    }
}
