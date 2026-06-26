using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations.StateMachines;
using Service1.Dtos;
using Service1.StateMachines.Events;

namespace Service1.StateMachines;

public class OrderStateMachine : StateMachine<OrderStateMachineInstance>
{
    // States declaration
    public IState OrderCreated { get; private set; }
    public IState OrderCancelled { get; private set; }
    public IState OrderSucceed { get; private set; }

    // Events declaration
    public IEvent<OrderCreated> OrderCreatedEvent { get; private set; }
    public IEvent<OrderCancelled> OrderCancelledEvent { get; private set; }
    public IEvent<OrderSucceed> OrderSucceedEvent { get; private set; }

    public OrderStateMachine(ILogger<OrderStateMachine> logger)
    {
        Event(OrderCreatedEvent, cfg =>
            cfg.CorrelationId(x => x.Message.OrderId));

        Event(OrderCancelledEvent, cfg =>
            cfg.CorrelationId(x => x.Message.OrderId));

        Event(OrderSucceedEvent, cfg => cfg
            .CorrelationBy((ins, ctx) => ins.OrderId == ctx.Message.OrderId));

        Initially(On(OrderCreatedEvent)
            .Then(context =>
            {
                context.Instance.OrderId = context.Message.OrderId;
                context.Instance.OrderName = context.Message.OrderName;
                context.Instance.OrderTime = DateTime.UtcNow;
                logger.LogInformation("Message received is: {@Message}", context.Message);
            })
            .ThenAsync(async (context, ct) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
                logger.LogInformation("Message instance is: {@Instance}", context.Instance);
            })
            .TransitionTo(OrderCreated)
            .Then(context => logger.LogInformation("After transition to new state: {@Instance}", context.Instance))
            .If(context => context.Instance.State == OrderCreated.Name,
                cb => cb.Then(context =>
                        logger.LogInformation("Hehe, this is the last instance: {@Instance}", context.Instance))
                    .ThenAsync(async (context, ct) =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), ct);
                        logger.LogWarning("Starting the complex thing, the context did not change: {@Context}",
                            context);
                    })
                    .TransitionTo(OrderCancelled)
                    .If(context => context.Instance.State == OrderCancelled.Name, ctx =>
                        ctx
                            .ThenAsync(async (_, ct) => await Task.Delay(TimeSpan.FromSeconds(5), ct))
                            .Then(context =>
                                logger.LogInformation("Ok, all good!: {@InstanceState}", context.Instance.State))
                    )
                    .IfElse(_ => false, _ => { }, elseCallback => elseCallback
                        .ThenAsync(async (_, ct) => await Task.Delay(TimeSpan.FromSeconds(3), ct))
                        .Then(context => logger.LogInformation("Seems we have done a lot of thing?: {@Instance}", context.Instance))
                        .Publish(ctx => new OrderPublisherTest
                        {
                            OrderId = ctx.Instance.OrderId
                        })
                    )
            )
        );
    }
}