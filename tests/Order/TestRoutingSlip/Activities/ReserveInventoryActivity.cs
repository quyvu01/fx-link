using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Contexts;
using Order.TestRoutingSlip.Arguments;
using Order.TestRoutingSlip.RoutingSlipLogs;

namespace Order.TestRoutingSlip.Activities;

public sealed class ReserveInventoryActivity(ILogger<ReserveInventoryActivity> logger)
    : IExecuteActivity<ReserveInventoryArgs, ReserveInventoryLog>
{
    public async Task<IExecuteResult<ReserveInventoryLog>> ExecuteAsync(
        IExecuteContext<ReserveInventoryArgs, ReserveInventoryLog> context, CancellationToken token = default)
    {
        logger.LogInformation("[ReserveInventoryActivity] args: {@Args}", context.Argument);
        await Task.Delay(TimeSpan.FromSeconds(1), token);
        var reservationId = Guid.NewGuid();
        logger.LogInformation("[ReserveInventoryActivity] reserved: {ReservationId}", reservationId);
        return context.Completed(new ReserveInventoryLog
        {
            Name = context.Argument.Name, ReservationId = reservationId
        });
    }

    public async Task<ICompensatedResult> CompensateAsync(ICompensateContext<ReserveInventoryLog> context,
        CancellationToken token = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), token);
        logger.LogInformation("[ReserveInventoryActivity] released reservation: {@Logs}", context.Log);
        return context.Compensated();
    }
}
