using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using Order.TestRoutingSlip.Arguments;
using Order.TestRoutingSlip.Contracts;

namespace Order.TestRoutingSlip.Builders;

public class TestOrderConsumer(ILogger<TestOrderConsumer> logger, IRoutingSlipExecutor executor)
    : IConsumer<IActiveRoutingSlip>
{
    public async Task ConsumeAsync(IConsumerContext<IActiveRoutingSlip> context, CancellationToken token = default)
    {
        await Task.Yield();
        var message = context.Message;
        logger.LogInformation("[TestOrderConsumer] message: {@Message}", message);
        await executor.RunAsync(cfg => cfg
            .AddArgument(new AddOrderArgs { Name = message.Name })
            .AddArgument(
                new ConfirmOrderArgs { Name = message.Name, IsFaultSimulation = message.IsFaultSimulation })
            .SetVariable("customerId", 123));
    }
}