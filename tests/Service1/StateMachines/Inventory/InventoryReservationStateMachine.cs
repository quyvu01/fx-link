using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations.StateMachines;
using Service1.Dtos.Inventory;
using Service1.StateMachines.Inventory.Activities;

namespace Service1.StateMachines.Inventory;

// EF Core-backed, Pessimistic concurrency (see Program.cs). Deliberately smaller than
// OrderStateMachine: its job is to isolate the Pessimistic-mode-only concerns instead of
// re-demonstrating everything OrderStateMachine already covers.
// Covers: CorrelationBy(...).SelectId(...) (required under Pessimistic mode so the advisory lock
// can be acquired before the instance is even read), and OnMissingInstance Fault()/ExecuteAsync().
public sealed class InventoryReservationStateMachine : StateMachine<InventoryReservationInstance>
{
    public IState Reserved { get; private set; }
    public IState Confirmed { get; private set; }

    public IEvent<ReserveInventory> ReserveInventoryEvent { get; private set; }
    public IEvent<ReleaseInventory> ReleaseInventoryEvent { get; private set; }
    public IEvent<ConfirmInventory> ConfirmInventoryEvent { get; private set; }

    public InventoryReservationStateMachine(ILogger<InventoryReservationStateMachine> logger)
    {
        Event(ReserveInventoryEvent, cfg => cfg.CorrelationId(x => x.Message.OrderId));

        // OnMissingInstance: Fault() -> releasing a reservation that never existed is an error,
        // not something to silently swallow.
        Event(ReleaseInventoryEvent, cfg =>
        {
            cfg.CorrelationBy((ins, ctx) => ins.OrderId == ctx.Message.OrderId && ins.Sku == ctx.Message.Sku)
                .SelectId(x => x.Message.OrderId);
            cfg.OnMissingInstance(x => x.Fault());
        });

        // OnMissingInstance: ExecuteAsync() -> asynchronous fallback (e.g. an audit log write).
        Event(ConfirmInventoryEvent, cfg =>
        {
            cfg.CorrelationId(x => x.Message.OrderId);
            cfg.OnMissingInstance(x => x.ExecuteAsync(async (context, ct) =>
            {
                await Task.Delay(10, ct);
                logger.LogWarning("Confirm requested for a reservation that no longer exists: {@Message}",
                    context.Message);
            }));
        });

        Initially(When(ReserveInventoryEvent)
            .Then(context =>
            {
                context.Instance.OrderId = context.Message.OrderId;
                context.Instance.Sku = context.Message.Sku;
                context.Instance.Quantity = context.Message.Quantity;
                context.Instance.ReservedAt = DateTime.UtcNow;
                logger.LogInformation("Inventory reserved: {@Message}", context.Message);
            })
            .TransitionTo(Reserved)
        );

        During(Reserved, When(ReleaseInventoryEvent)
            .Then(ctx => logger.LogInformation("Releasing reservation: {@Message}", ctx.Message))
            .Complete()
        );

        During(Reserved, When(ConfirmInventoryEvent)
            .Then(ctx => logger.LogInformation("Confirming reservation: {@Message}", ctx.Message))
            .Activity(c => c.OfInstanceType<InventoryConfirmationActivity>())
        );

        RemoveInstanceWhenCompleted();
    }
}
