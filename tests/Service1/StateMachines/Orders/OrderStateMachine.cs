using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations.StateMachines;
using Service1.Dtos.Orders;
using Service1.StateMachines.Orders.Activities;

namespace Service1.StateMachines.Orders;

// EF Core-backed, Optimistic concurrency (see Program.cs). Covers the bulk of the FlowOperator
// DSL: Then/ThenAsync, If/IfAsync/IfElseAsync, TransitionTo/Complete, Publish/PublishAsync,
// Response/ResponseAsync, Schedule/Unschedule, Request/RequestAsync (Completed/Failed/TimeoutExpired),
// Activity (both OfType and OfInstanceType), DuringAny, and every OnMissingInstance variant except
// Fault/ExecuteAsync (see InventoryReservationStateMachine for those).
public sealed class OrderStateMachine : StateMachine<OrderStateMachineInstance>
{
    // States declaration
    public IState OrderCreated { get; private set; }
    public IState OrderCancelled { get; private set; }
    public IState OrderSucceed { get; private set; }
    public IState OrderInRequesting { get; private set; }

    // Events declaration
    public IEvent<OrderCreated> OrderCreatedEvent { get; private set; }
    public IEvent<OrderCancelled> OrderCancelledEvent { get; private set; }
    public IEvent<OrderReactivated> OrderReactivatedEvent { get; private set; }
    public IEvent<GetOrderStats> GetOrderStatsEvent { get; private set; }
    public IEvent<GetOrderSummary> GetOrderSummaryEvent { get; private set; }
    public ISchedule<OrderScheduler> OrderScheduler { get; private set; }
    public IRequest<GetOrderHistory, OrderHistoryResponse> GetOrderHistory { get; private set; }

    public OrderStateMachine(ILogger<OrderStateMachine> logger)
    {
        Event(OrderCreatedEvent, cfg =>
            cfg.CorrelationId(x => x.Message.OrderId));

        // OnMissingInstance: Discard() -> cancelling an order that was never created is a silent no-op.
        Event(OrderCancelledEvent, cfg =>
        {
            cfg.CorrelationId(x => x.Message.OrderId);
            cfg.OnMissingInstance(x => x.Discard());
        });

        // CorrelationBy without SelectId: fine here because this event is only ever raised During(...),
        // never Initially(...), so no new-instance correlation id ever needs to be resolved for it.
        Event(OrderReactivatedEvent, cfg => cfg
            .CorrelationBy((ins, ctx) => ins.OrderId == ctx.Message.OrderId));

        // OnMissingInstance: Execute() -> synchronous fallback response.
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

        // Bound only via DuringAny(...) below (never Initially), so a missing instance must be
        // handled explicitly too.
        Event(GetOrderSummaryEvent, cfg =>
        {
            cfg.CorrelationId(x => x.Message.OrderId);
            cfg.OnMissingInstance(x => x
                .Execute(context => context.ResponseAsync(new OrderSummaryResponse
                {
                    OrderId = context.Message.OrderId,
                    State = "Unknown - no instance found",
                    SummarizedAt = DateTime.UtcNow
                })));
        });

        Schedule(OrderScheduler, opts =>
        {
            opts.Delay = TimeSpan.FromSeconds(5);
            opts.TokenIdProvider = o => o.MonitorTokenTimeout;
            opts.Received = x => x.CorrelationId(c => c.Message.OrderId);
        });

        Request(GetOrderHistory, opts =>
        {
            opts.TimeToLive = TimeSpan.FromSeconds(5);
            opts.Timeout = TimeSpan.FromSeconds(10);
            opts.Completed = ev => ev.CorrelationBy((ins, ctx) => ins.OrderId == ctx.Message.OrderId);
            opts.Failed = ev => ev.CorrelationId(x => x.Message.Message.OrderId);
            opts.TimeoutExpired = ev => ev.CorrelationId(x => x.Message.Message.OrderId);
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
            .Schedule(OrderScheduler, ctx => new OrderScheduler { OrderId = ctx.Instance.OrderId })
            .Then(context => logger.LogInformation("After transition to new state: {@Instance}", context.Instance))
            // If (sync, no else branch) - just an extra side effect, flow continues either way.
            .If(ctx => ctx.Message.OrderName is { Length: > 20 },
                s => s.Then(c => logger.LogInformation("Order has an unusually long name: {@Name}",
                    c.Message.OrderName)))
            // IfElseAsync: same branching as If/IfElse, but the condition itself is awaited first
            // (e.g. simulating a fraud-check call).
            .IfElseAsync(async (ctx, ct) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
                    return ctx.Message.RandomNumber > 3;
                },
                succeed => succeed
                    .TransitionTo(OrderSucceed)
                    .Then(c => logger.LogInformation("Succeed, instance: {@State}", c.Instance))
                    // No longer need the reminder schedule once the order has already succeeded.
                    .Unschedule(OrderScheduler),
                otherwise => otherwise
                    .TransitionTo(OrderCancelled)
                    .ThenAsync(async (_, ct) => await Task.Delay(2000, ct))
                    .Then(c => logger.LogInformation("Cancelled, instance: {@State}", c.Instance))
            ));

        During(OrderCreated, When(OrderCancelledEvent)
            .Then(context => logger.LogInformation("Order cancelled directly: {@Message}", context.Message))
            .IfElse(ctx => ctx.Message.CancelledTime > DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                succeed => succeed
                    .TransitionTo(OrderSucceed)
                    .Publish(_ => new OrderCancelled())
                , otherwise => otherwise
                    .TransitionTo(OrderCancelled)
                    .Publish(_ => new OrderPlaced())
                    .IfElse(_ => true, x => x, f => f)
            )
            .TransitionTo(OrderCancelled)
        );

        During(OrderCancelled, When(OrderScheduler.Received)
            .Then(ctx => logger.LogInformation("OrderScheduler.Received received: {@Message}", ctx.Message))
            .TransitionTo(OrderInRequesting)
            .Then(ctx => logger.LogInformation("Changed state to: {@State}", ctx.Instance.State))
        );

        During(OrderInRequesting, When(OrderCreatedEvent)
            .Then(ctx => logger.LogInformation("Start testing request flow: {@Message}", ctx.Message))
            .RequestAsync(GetOrderHistory, async (ctx, ct) =>
            {
                await Task.Delay(5, ct);
                return new GetOrderHistory { OrderId = ctx.Instance.OrderId, ForceFail = ctx.Message.RandomNumber < 0 };
            })
            .TransitionTo(GetOrderHistory.Pending)
        );

        During(GetOrderHistory.Pending,
            When(GetOrderHistory.TimeoutExpired)
                .Then(ctx => logger.LogInformation("Timeout for request: {@Message}", ctx.Message))
                .TransitionTo(OrderCancelled),
            When(GetOrderHistory.Completed)
                .Then(ctx => logger.LogInformation("Completed for request: {@Message}", ctx.Message))
                .Activity(c => c.OfInstanceType<OrderHistoryReviewActivity>()),
            When(GetOrderHistory.Failed)
                .Then(ctx => logger.LogWarning("Order history lookup failed: {@Exceptions}", ctx.Message.Exceptions))
                .TransitionTo(OrderCancelled)
        );

        During(OrderCancelled, When(OrderReactivatedEvent)
            .Then(context => logger.LogInformation("Reactivate cancelled order: {@Message}", context.Message))
            // IfAsync (no else): just an extra async guard before the activity runs.
            .IfAsync(async (ctx, ct) =>
                {
                    await Task.Delay(20, ct);
                    return ctx.Message.OrderId != Guid.Empty;
                },
                s => s.Then(c => logger.LogInformation("Reactivation guard passed for {@OrderId}",
                    c.Message.OrderId)))
            // Message-typed activity: performs its own TranslationTo(OrderSucceed).
            .Activity(c => c.OfType<OrderReactivationActivity>())
            .PublishAsync(async (ctx, ct) =>
            {
                await Task.Delay(10, ct);
                return new OrderSucceed { OrderId = ctx.Instance.OrderId };
            })
        );

        During(OrderCreated, OrderCancelled, When(GetOrderStatsEvent)
            .Response(c => new OrderStatsResponse
            {
                OrderId = c.Instance.OrderId,
                OrderName = c.Instance.OrderName,
                State = c.Instance.State
            })
        );

        // Same event (OrderCancelledEvent), different state -> different flow: here it means
        // "archive the succeeded order", finishing at the built-in Completed state.
        During(OrderSucceed, When(OrderCancelledEvent)
            .Then(ctx => logger.LogInformation("Archiving succeeded order: {@Message}", ctx.Message))
            .Complete()
        );

        // DuringAny(...) binds to every declared state (not Initial/Completed), so the summary can
        // be requested regardless of where the order currently is.
        DuringAny(When(GetOrderSummaryEvent)
            .ResponseAsync(async (ctx, ct) =>
            {
                await Task.Delay(20, ct);
                return new OrderSummaryResponse
                {
                    OrderId = ctx.Instance.OrderId, State = ctx.Instance.State, SummarizedAt = DateTime.UtcNow
                };
            })
        );

        RemoveInstanceWhenCompleted();
    }
}