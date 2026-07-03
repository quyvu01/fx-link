using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations.StateMachines;
using Service1.Dtos;
using Service1.StateMachines.Events;
using Service1.StateMachines.Schedules;

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
    public IEvent<OrderReactivated> OrderReactivatedEvent { get; private set; }
    public IEvent<GetOrderStats> GetOrderStatsEvent { get; private set; }
    public ISchedule<OrderScheduler> OrderScheduler { get; set; }

    public OrderStateMachine(ILogger<OrderStateMachine> logger)
    {
        Event(OrderCreatedEvent, cfg =>
            cfg.CorrelationId(x => x.Message.OrderId));

        Event(OrderCancelledEvent, cfg =>
            cfg.CorrelationId(x => x.Message.OrderId));

        Event(OrderReactivatedEvent, cfg => cfg
            .CorrelationBy((ins, ctx) => ins.OrderId == ctx.Message.OrderId));

        Event(GetOrderStatsEvent, cfg =>
        {
            cfg.CorrelationId(x => x.Message.OrderId);
            cfg.OnMissingInstance(x => x
                .Execute(context => context.ResponseAsync(new OrderStatsResponse
                {
                    OrderId = context.Message.OrderId,
                    OrderName = "[Kidding me?, no name]",
                    State = "Nooo, no instance -> No state"
                })));
        });

        Schedule(OrderScheduler, opts =>
        {
            opts.Delay = TimeSpan.FromSeconds(5);
            opts.TokenIdProvider = o => o.MonitorTokenTimeout;
            opts.Received = x => x.CorrelationId(c => c.Message.OrderId);
        });

        Initially(When(OrderCreatedEvent)
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
            .Schedule(OrderScheduler, ctx
                => new OrderScheduler { OrderId = ctx.Instance.OrderId })
            .Then(context => logger.LogInformation("After transition to new state: {@Instance}", context.Instance))
            .Unschedule(OrderScheduler)
            .IfElse(ctx => ctx.Message.RandomNumber > 3,
                succeed => succeed
                    .TransitionTo(OrderSucceed)
                    .Then(c => logger.LogInformation("Succeed, instance: {@State}", c.Instance)),
                otherwise => otherwise
                    .TransitionTo(OrderCancelled)
                    .ThenAsync(async (_, ct) => await Task.Delay(2000, ct))
                    .Then(c => logger.LogInformation("Cancelled, instance: {@State}", c.Instance))
            ));

        During(OrderCancelled, When(OrderScheduler.Received)
            .Then(ctx => logger.LogInformation("OrderScheduler.Received received: {@Message}", ctx.Message))
        );

        During(OrderCancelled, When(OrderReactivatedEvent)
            .Then(context => logger.LogInformation("Reactive cancelled order: {@OrderMessage}", context.Message))
            .TransitionTo(OrderSucceed)
            .Publish(ctx => new OrderSucceed { OrderId = ctx.Instance.OrderId })
        );

        During(OrderCreated, OrderCancelled, When(GetOrderStatsEvent)
            .Response(c => new OrderStatsResponse
            {
                OrderId = c.Instance.OrderId,
                OrderName = c.Instance.OrderName,
                State = c.Instance.State
            })
        );
        RemoveInstanceWhenCompleted();
    }
}