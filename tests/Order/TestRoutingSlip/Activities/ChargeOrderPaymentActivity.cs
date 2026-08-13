using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Contexts;
using Order.TestRoutingSlip.Arguments;
using Order.TestRoutingSlip.RoutingSlipLogs;

namespace Order.TestRoutingSlip.Activities;

public sealed class ChargeOrderPaymentActivity(ILogger<ChargeOrderPaymentActivity> logger)
    : IExecuteActivity<ChargeOrderPaymentArgs, ChargeOrderPaymentLog>
{
    public async Task<IExecuteResult<ChargeOrderPaymentLog>> ExecuteAsync(
        IExecuteContext<ChargeOrderPaymentArgs, ChargeOrderPaymentLog> context, CancellationToken token = default)
    {
        logger.LogInformation("[ChargeOrderPaymentActivity] args: {@Args}", context.Argument);
        await Task.Delay(TimeSpan.FromSeconds(1), token);
        var transactionId = Guid.NewGuid().ToString("N");
        logger.LogInformation("[ChargeOrderPaymentActivity] charged: {TransactionId}", transactionId);
        return context.Completed(new ChargeOrderPaymentLog
        {
            Name = context.Argument.Name, Amount = context.Argument.Amount, TransactionId = transactionId
        });
    }

    public async Task<ICompensatedResult> CompensateAsync(ICompensateContext<ChargeOrderPaymentLog> context,
        CancellationToken token = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), token);
        logger.LogInformation("[ChargeOrderPaymentActivity] refunded: {@Logs}", context.Log);
        return context.Compensated();
    }
}
