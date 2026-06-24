using FxLink.StateMachine.Abstractions;
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
    public IEvent<OrderCancelled> OrderSucceedEvent { get; private set; }

    public OrderStateMachine(ILogger<OrderStateMachine> logger)
    {
        State(o => o.CurrentState);

        Event(OrderCreatedEvent, cfg =>
            cfg.CorrelationId(x => x.Message.OrderId));
        
        Event(OrderCancelledEvent, cfg =>
            cfg.CorrelationId(x => x.Message.OrderId));
        
        Event(OrderSucceedEvent, cfg => cfg
            .CorrelationBy((ins, ctx) => ins.OrderId == ctx.Message.OrderId));

        Initially(On(OrderCreatedEvent)
            .Then((ins, ctx) =>
            {
                ins.OrderId = ctx.Message.OrderId;
                ins.OrderName = ctx.Message.OrderName;
                logger.LogInformation("Message received is: {@Message}", ctx.Message);
            })
        );
        
        // During(Initial, On(OrderCreatedEvent)
        //     .Then((ins, ctx) =>
        //     {
        //         ins.OrderId = ctx.Message.OrderId;
        //         ins.OrderName = ctx.Message.OrderName;
        //         logger.LogInformation("Message received is: {@Message}", ctx.Message);
        //     })
        // );
    }
}