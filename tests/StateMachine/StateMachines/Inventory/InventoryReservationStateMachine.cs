using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations.StateMachines;
using StateMachine.Dtos.Inventory;
using StateMachine.StateMachines.Inventory.Activities;

namespace StateMachine.StateMachines.Inventory;

// EF Core-backed, Pessimistic concurrency (see Program.cs). The one state machine in this sample -
// covers the full EventOperator DSL: Then/ThenAsync, If/IfAsync/IfElse/IfElseAsync,
// TransitionTo/Complete, Publish/PublishAsync, Response/ResponseAsync, Schedule/Unschedule,
// Request/RequestAsync (Completed/Failed/TimeoutExpired), Activity (both OfType and
// OfInstanceType), DuringAny, every OnMissingInstance variant (Discard/Execute/ExecuteAsync/Fault),
// and CorrelationBy(...).SelectId(...) (required under Pessimistic mode so the advisory lock can be
// acquired before the instance is even read).
public sealed class InventoryReservationStateMachine : StateMachine<InventoryReservationInstance>
{
    // States declaration
    public IState Reserved { get; private set; }
    public IState Confirmed { get; private set; }
    public IState Cancelled { get; private set; }

    // Events declaration
    public IEvent<ReserveInventory> ReserveInventoryEvent { get; private set; }
    public IEvent<ReleaseInventory> ReleaseInventoryEvent { get; private set; }
    public IEvent<ConfirmInventory> ConfirmInventoryEvent { get; private set; }
    public IEvent<CancelSchedule> CancelScheduleEvent { get; private set; }
    public IEvent<AdjustStock> AdjustStockEvent { get; private set; }
    public IEvent<GetReservationStats> GetReservationStatsEvent { get; private set; }
    public IEvent<GetReservationSummary> GetReservationSummaryEvent { get; private set; }
    public ISchedule<InventorySchedule> InventorySchedule { get; private set; }
    public IRequest<CheckWarehouseStock, WarehouseStockResponse> CheckWarehouseStock { get; private set; }

    public InventoryReservationStateMachine(ILogger<InventoryReservationStateMachine> logger)
    {
        Event(ReserveInventoryEvent, cfg => cfg.CorrelationId(x => x.Message.OrderId));

        // CorrelationBy(...).SelectId(...): Pessimistic mode needs the id up front to acquire the
        // advisory lock before the instance is loaded.
        Event(ReleaseInventoryEvent, cfg =>
        {
            cfg.CorrelationBy((ins, ctx) => ins.OrderId == ctx.Message.OrderId && ins.Sku == ctx.Message.Sku)
                .SelectId(x => x.Message.OrderId);
            // OnMissingInstance: Fault() -> releasing a reservation that never existed is an error,
            // not something to silently swallow.
            cfg.OnMissingInstance(x => x.Fault());
        });

        Event(ConfirmInventoryEvent, cfg =>
        {
            cfg.CorrelationId(x => x.Message.OrderId);
            // OnMissingInstance: ExecuteAsync() -> asynchronous fallback (e.g. an audit log write).
            cfg.OnMissingInstance(x => x.ExecuteAsync(async (context, ct) =>
            {
                await Task.Delay(10, ct);
                logger.LogWarning("Confirm requested for a reservation that no longer exists: {@Message}",
                    context.Message);
            }));
        });

        Event(CancelScheduleEvent, cfg =>
        {
            cfg.CorrelationId(x => x.Message.OrderId);
            // OnMissingInstance: Discard() -> cancelling a schedule for a reservation that's already
            // gone is a silent no-op.
            cfg.OnMissingInstance(x => x.Discard());
        });

        Event(AdjustStockEvent, cfg => cfg.CorrelationId(x => x.Message.OrderId));

        // OnMissingInstance: Execute() -> synchronous fallback response.
        Event(GetReservationStatsEvent, cfg =>
        {
            cfg.CorrelationId(x => x.Message.OrderId);
            cfg.OnMissingInstance(x => x
                .Execute(context => context.ResponseAsync(new ReservationStatsResponse
                {
                    OrderId = context.Message.OrderId,
                    State = "Unknown - no reservation found"
                })));
        });

        // Bound only via DuringAny(...) below (never Initially), so a missing instance must be
        // handled explicitly too.
        Event(GetReservationSummaryEvent, cfg =>
        {
            cfg.CorrelationId(x => x.Message.OrderId);
            cfg.OnMissingInstance(x => x
                .Execute(context => context.ResponseAsync(new ReservationSummaryResponse
                {
                    OrderId = context.Message.OrderId,
                    State = "Unknown - no reservation found",
                    SummarizedAt = DateTime.UtcNow
                })));
        });

        Schedule(InventorySchedule, cfg =>
        {
            cfg.Delay = TimeSpan.FromSeconds(15);
            cfg.Received = x => x.CorrelationId(k => k.Message.OrderId);
            cfg.TokenIdProvider = i => i.TokenId;
        });

        Request(CheckWarehouseStock, opts =>
        {
            opts.TimeToLive = TimeSpan.FromSeconds(5);
            opts.Timeout = TimeSpan.FromSeconds(10);
            opts.Completed = ev => ev
                .CorrelationBy((ins, ctx) => ins.OrderId == ctx.Message.OrderId);
            opts.Failed = ev => ev.CorrelationId(x => x.Message.Message.OrderId);
            opts.TimeoutExpired = ev => ev.CorrelationId(x => x.Message.Message.OrderId);
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
            .ThenAsync(async (context, ct) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
                logger.LogInformation("Reservation instance is: {@Instance}", context.Instance);
            })
            .Schedule(InventorySchedule, x => new InventorySchedule { OrderId = x.Message.OrderId })
            .TransitionTo(Reserved)
            // If (sync, no else branch) - just an extra side effect, flow continues either way.
            .If(ctx => ctx.Message.Quantity > 100,
                s => s.Then(c => logger.LogInformation("Bulk reservation: {@Quantity}", c.Message.Quantity)))
        );

        During(Reserved,
            When(ReleaseInventoryEvent)
                .Then(ctx => logger.LogInformation("Releasing reservation: {@Message}", ctx.Message))
                .IfElse(ctx => ctx.Message.Quantity >= ctx.Instance.Quantity,
                    full => full
                        .Unschedule(InventorySchedule)
                        .Publish(ctx => new InventoryReleased { OrderId = ctx.Instance.OrderId })
                        .Complete(),
                    partial => partial
                        .Then(ctx =>
                        {
                            ctx.Instance.Quantity -= ctx.Message.Quantity;
                            logger.LogInformation("Partial release, remaining: {@Quantity}", ctx.Instance.Quantity);
                        })
                ),
            When(InventorySchedule.Received)
                .Then(ctx => logger.LogInformation("[InventorySchedule.Received] received: {@Message}", ctx.Message)),
            When(CancelScheduleEvent)
                .Then(ctx => logger.LogInformation("[CancelScheduleEvent] received: {@Message}", ctx.Message))
                .Unschedule(InventorySchedule),
            When(AdjustStockEvent)
                // IfAsync (no else): an extra async guard before the activity runs.
                .IfAsync(async (ctx, ct) =>
                    {
                        await Task.Delay(20, ct);
                        return ctx.Message.NewQuantity >= 0;
                    },
                    s => s.Then(c => logger.LogInformation("Adjustment guard passed for {@OrderId}",
                        c.Message.OrderId)))
                .Activity(c => c.OfType<StockAdjustmentActivity>())
        );

        During(Reserved, When(ConfirmInventoryEvent)
            .Then(ctx => logger.LogInformation("Confirming reservation: {@Message}", ctx.Message))
            .RequestAsync(CheckWarehouseStock, async (ctx, ct) =>
            {
                await Task.Delay(5, ct);
                return new CheckWarehouseStock { OrderId = ctx.Instance.OrderId, Sku = ctx.Instance.Sku };
            })
            .TransitionTo(CheckWarehouseStock.Pending)
        );

        During(CheckWarehouseStock.Pending,
            When(CheckWarehouseStock.TimeoutExpired)
                .Then(ctx => logger.LogInformation("Warehouse check timed out: {@Message}", ctx.Message))
                .TransitionTo(Cancelled),
            When(CheckWarehouseStock.Completed)
                .Then(ctx => logger.LogInformation("Warehouse check completed: {@Message}", ctx.Message))
                // IfElseAsync: same branching as If/IfElse, but the condition itself is awaited first
                // (e.g. simulating a downstream stock recheck).
                .IfElseAsync(async (ctx, ct) =>
                    {
                        await Task.Delay(20, ct);
                        return ctx.Message.InStock;
                    },
                    inStock => inStock
                        // Instance-typed activity: performs its own TranslationTo(Confirmed).
                        .Activity(c => c.OfInstanceType<InventoryConfirmationActivity>()),
                    outOfStock => outOfStock
                        .TransitionTo(Cancelled)
                        .PublishAsync(async (ctx2, ct2) =>
                        {
                            await Task.Delay(10, ct2);
                            return new InventoryReleased { OrderId = ctx2.Instance.OrderId };
                        })
                ),
            When(CheckWarehouseStock.Failed)
                .Then(ctx => logger.LogWarning("Warehouse check failed: {@Exceptions}", ctx.Message.Exceptions))
                .TransitionTo(Cancelled)
        );

        During(Reserved, Confirmed, When(GetReservationStatsEvent)
            .Response(c => new ReservationStatsResponse
            {
                OrderId = c.Instance.OrderId,
                Sku = c.Instance.Sku,
                Quantity = c.Instance.Quantity,
                State = c.Instance.State
            })
        );

        // DuringAny(...) binds to every declared state (not Initial/Completed), so the summary can
        // be requested regardless of where the reservation currently is.
        DuringAny(When(GetReservationSummaryEvent)
            .ResponseAsync(async (ctx, ct) =>
            {
                await Task.Delay(20, ct);
                return new ReservationSummaryResponse
                {
                    OrderId = ctx.Instance.OrderId, State = ctx.Instance.State, SummarizedAt = DateTime.UtcNow
                };
            })
        );

        RemoveInstanceWhenCompleted();
    }
}