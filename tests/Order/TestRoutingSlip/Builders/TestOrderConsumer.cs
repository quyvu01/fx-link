using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Extensions;
using Order.TestRoutingSlip.Arguments;
using Order.TestRoutingSlip.Contracts;

namespace Order.TestRoutingSlip.Builders;

public class TestOrderConsumer(ILogger<TestOrderConsumer> logger, IRoutingSlipExecutor executor)
    : IConsumer<IActiveRoutingSlip>
{
    public async Task ConsumeAsync(IConsumeContext<IActiveRoutingSlip> context, CancellationToken token = default)
    {
        var message = context.Message;
        logger.LogInformation("[TestOrderConsumer] message: {@Message}", message);

        // 5-step order saga: ReserveInventory -> AddOrder -> ChargeOrderPayment (all compensatable)
        // -> ConfirmOrder (fault-simulation point, no logs) -> NotifyCustomer (no logs, terminal).
        // isFaultSimulation=true faults at ConfirmOrder, so the backward-walk has 3 real entries to
        // compensate: ChargeOrderPayment -> AddOrder -> ReserveInventory. NotifyCustomer never runs.
        await executor.RunAsync(cfg => cfg
            .AddArgument(new ReserveInventoryArgs { Name = message.Name, Quantity = 1 })
            .AddArgument(new Uri("queue:reverse-inventory-args"), new { message.Name })
            .AddArgument(new AddOrderArgs { Name = message.Name })
            .AddArgument(new ChargeOrderPaymentArgs { Name = message.Name, Amount = 100 })
            .AddArgument(new ConfirmOrderArgs { Name = message.Name, IsFaultSimulation = message.IsFaultSimulation })
            .AddArgument(new NotifyCustomerArgs { Name = message.Name })
            .SetVariable("customerId", 123), token);
    }
}