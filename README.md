<p align="center">
  <img src="FxLink.png" alt="FxLink" width="120" />
</p>

<h1 align="center">FxLink</h1>

<p align="center">
  A distributed messaging framework for .NET — pub/sub, request/response, sagas and reliable delivery,
  on the broker you already run.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/FxLink"><img alt="NuGet" src="https://img.shields.io/nuget/v/FxLink.svg" /></a>
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-Apache--2.0-blue.svg" /></a>
</p>

---

FxLink lets services talk to each other through typed messages without coupling your business code to a
specific broker. You write consumers and publish messages; FxLink handles serialization, routing,
retries, dead-lettering and delivery guarantees, and a small transport package binds it all to
RabbitMQ, Amazon SNS/SQS or NATS JetStream (or runs everything in-process for tests).

## Features

- **Publish/subscribe** with fan-out: every consumer type gets its own copy of a message.
- **Request/response** (`IRequester<T>`) over any transport, with timeouts.
- **Consumer pipeline** with pluggable behaviors — retries with backoff, dead-lettering, batching,
  outbox/inbox — configured per consumer or per message.
- **Interface contracts**: publish anonymous objects against an interface and consume them on the other
  side without sharing a class (`PublishAsync<IOrderCreated>(new { ... })`).
- **Transactional Outbox** and **idempotent Inbox** (in-memory or EF Core) for exactly-once *effects* on
  top of at-least-once delivery.
- **State machines** (sagas) with persistence, correlation, schedules and request/response steps.
- **Routing slips** with automatic compensation when a step fails.
- **Supervision**: a broker connector that fails is restarted with backoff and an optional circuit breaker.
- Targets **.NET 8, 9 and 10**.

## Packages

| Package | What it is |
|---|---|
| `FxLink` | Core: messages, consumers, publisher, requester, pipeline, retry, batching, Outbox/Inbox abstractions, in-memory transport and stores. |
| `FxLink.RabbitMq` | RabbitMQ transport. |
| `FxLink.Aws.Sqs` | Amazon SNS + SQS transport. |
| `FxLink.Nats` | NATS JetStream transport. |
| `FxLink.Messaging.EntityFrameworkCore` | EF Core stores for the Outbox and the Inbox. |
| `FxLink.StateMachine` | State machines / sagas. |
| `FxLink.StateMachine.EntityFrameworkCore` | EF Core persistence for state machine instances. |
| `FxLink.RoutingSlip` | Routing slips with compensation. |

You need the core package, exactly one transport, and whichever of the rest you use.

```bash
dotnet add package FxLink
dotnet add package FxLink.RabbitMq          # or FxLink.Aws.Sqs / FxLink.Nats
dotnet add package FxLink.Messaging.EntityFrameworkCore   # optional: Outbox / Inbox
```

## Quick start

A message is any class (or interface). A consumer implements `IConsumer<TMessage>`:

```csharp
public sealed class OrderPlaced
{
    public Guid OrderId { get; set; }
}

public sealed class OrderPlacedConsumer(ILogger<OrderPlacedConsumer> logger) : IConsumer<OrderPlaced>
{
    public Task ConsumeAsync(IConsumeContext<OrderPlaced> context, CancellationToken token = default)
    {
        logger.LogInformation("Order placed: {OrderId}", context.Message.OrderId);
        return Task.CompletedTask;
    }
}
```

Register FxLink and a transport:

```csharp
builder.Services.AddFxLink(opts =>
{
    opts.AddConsumersFromAssemblies(typeof(Program).Assembly);
    opts.AddRabbitMq(rabbit => rabbit.Host("localhost", "/"));
});
```

Publish from anywhere:

```csharp
app.MapPost("/orders", async (IPublisher publisher) =>
{
    await publisher.PublishAsync(new OrderPlaced { OrderId = Guid.NewGuid() });
    return Results.Accepted();
});
```

No broker at hand? `opts.UseInMemory()` runs the whole pipeline in-process — handy for tests and
single-process apps.

## Core concepts

### Request/response

Ask another service a question and await the answer. The consumer replies with `ResponseAsync`:

```csharp
public sealed class StockConsumer : IConsumer<CheckStock>
{
    public Task ConsumeAsync(IConsumeContext<CheckStock> context, CancellationToken token = default) =>
        context.ResponseAsync(new StockLevel { Sku = context.Message.Sku, Quantity = 12 }, token);
}

// caller
var reply = await requester.RequestAsync<StockLevel>(new CheckStock { Sku = "abc" }, token);
```

Requests time out on the caller's side (`RequestContext.Timeout`).

### Interface contracts

Services can agree on an interface and never share an implementation:

```csharp
public interface IOrderCreated { Guid OrderId { get; } decimal Price { get; } }

await publisher.PublishAsync<IOrderCreated>(new { OrderId = id, Price = 10m });
// ...and elsewhere: public class Handler : IConsumer<IOrderCreated> { ... }
```

### Retries and dead-lettering

A consumer that throws is retried automatically with backoff (default 2s, 4s, 8s). When the retries
are exhausted — or the exception is one you marked as ignored — the message is dead-lettered.
Customize this with a consumer definition:

```csharp
public sealed class PaymentsConsumerDefinition : ConsumerDefinition<PaymentsConsumer>
{
    public override void Configure(IConsumerConfigurator<PaymentsConsumer> options) =>
        options.UseMessageRetry(retry =>
        {
            retry.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
            retry.Ignore<InvalidDataException>();   // straight to the dead-letter, no retries
        });
}

opts.AddConsumerDefinitionsFromAssemblies(typeof(Program).Assembly);
```

### Batching

Consume messages in groups by consuming `IBatch<T>`:

```csharp
public sealed class InventoryBatchConsumer : IConsumer<IBatch<IInventoryCreated>>
{
    public Task ConsumeAsync(IConsumeContext<IBatch<IInventoryCreated>> context, CancellationToken token = default)
    {
        foreach (var message in context.Message) { /* ... */ }
        return Task.CompletedTask;
    }
}

// in the consumer's definition
options.UseBatching<IInventoryCreated>(batch => batch
    .GroupBy(x => x.Message.Name)
    .SetMessageLimit(3)
    .SetTimeLimit(TimeSpan.FromSeconds(5)));
```

### Message definitions

Override how a message is named on the broker, or opt into plain-JSON serialization for messages produced
by non-FxLink systems:

```csharp
public sealed class CalendarCreatedDefinition : MessageDefinition<ICalendarCreated>
{
    public override void Configure(IMessageConfigurator<ICalendarCreated> options) =>
        options.Name("calendar.created");
    // options.UseRawJsonSerializer();
}

opts.AddMessageDefinitionsFromAssemblies(typeof(Program).Assembly);
```

### Supervision

Each transport runs as a supervised connector. If it fails it is restarted with exponential backoff:

```csharp
opts.ConfigureSupervisor(s =>
{
    s.Strategy = SupervisionStrategy.OneForOne;
    s.MaxRestarts = 5;
    s.EnableCircuitBreaker = true;
});
```

## Transports

| | RabbitMQ | Amazon SNS/SQS | NATS JetStream |
|---|---|---|---|
| Fan-out | fanout exchange per message type, queue per consumer | SNS topic per message type, SQS queue per consumer | one stream, subject per message type, durable consumer per consumer type |
| Request/response | exclusive reply queue per instance | unique reply queue per instance | unique `_INBOX` subject per instance (Core NATS) |
| Retry backoff | TTL + dead-letter queue | not yet implemented | server-side message scheduling |
| Dead-letter | dead-letter exchange + queue | native redrive policy as a safety net | dead-letter subject retained by a per-consumer durable |
| Delayed publish | delayed-message-exchange plugin | `DelaySeconds`, up to 15 minutes | server-side message scheduling (NATS ≥ 2.12) |

All transports honor custom message names (`MessageDefinition`), custom consumer endpoint names
(`options.ReceivedEndpoint("...")` in a consumer definition) and the same consumer pipeline.

### RabbitMQ

```csharp
opts.AddRabbitMq(rabbit =>
{
    rabbit.Host("localhost", "/", port: 5672, credential =>
    {
        credential.UserName("guest");
        credential.Password("guest");
    });
    rabbit.PrefetchCount(16);
    rabbit.ConcurrentMessageLimit(8);
});
opts.UseRabbitMqDelayScheduler();   // optional: needs rabbitmq_delayed_message_exchange
```

### Amazon SNS + SQS

Publishing goes to an SNS topic; each consumer reads its own SQS queue subscribed to the topics it
consumes. Queues, topics, subscriptions, access policies and dead-letter queues are created idempotently
at startup.

```csharp
opts.AddSqs(sqs =>
{
    sqs.Region(RegionEndpoint.USEast1, credential =>
    {
        // Omit for the default AWS credential chain. ServiceUrl targets LocalStack.
        credential.ServiceUrl("http://localhost:4566");
        credential.AccessKeyId("test");
        credential.SecretAccessKey("test");
    });
    sqs.MaxReceiveCount(5);       // redeliveries before SQS moves a message to the dead-letter queue
});
opts.UseSqsDelayScheduler();      // optional: delayed publish (max 15 minutes)
```

Note: SQS retry backoff and app-level dead-lettering are not implemented yet — a consumer that throws
fails the delivery instead of being retried by FxLink.

### NATS JetStream

Needs a server with JetStream enabled (`nats-server -js`); retry and delay need server 2.12 or newer.

```csharp
opts.AddNats(nats =>
{
    nats.Server("nats://localhost:4222", credential =>
    {
        // credential.UserName / Password / Token / CredsFile / Tls(...)
    });
    nats.StreamName("FXLINK");    // one shared stream...
    nats.SubjectPrefix("fxlink"); // ...capturing "fxlink.>"
    nats.MaxDeliver(5);
    nats.AckWait(TimeSpan.FromSeconds(30));
    nats.MaxAckPending(100);
    nats.MaxAge(TimeSpan.FromDays(7));
    // nats.AutoProvision(false);  // if streams/consumers are managed outside the app
});
opts.UseNatsDelayScheduler();     // optional: delayed publish
```

FxLink publishes through JetStream, so a publish completes only after the server has stored the message.
Messages that exhaust `MaxDeliver` (a consumer crashing mid-handling) stop being delivered rather than
being dead-lettered.

## Reliable messaging: Outbox and Inbox

Publishing to a broker and writing to your database are two operations that can fail independently.
The **Outbox** stores outgoing messages in the same transaction as your business data and dispatches
them afterwards, in order per correlation id. The **Inbox** is its counterpart on the consuming side: it
records that a consumer has handled a message so a redelivery does not repeat its side effects.

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutboxMessageEntity();   // FxLink.Messaging.EntityFrameworkCore.Extensions
        modelBuilder.AddInboxRecordEntity();
    }
}

opts.UseOutbox(outbox =>
{
    outbox.EntityFrameworkOutbox(ef => ef.AddDbContext<AppDbContext>());
    outbox.DispatcherOptions(d => d.PollInterval = TimeSpan.FromSeconds(2));
    // or scope it to one message type:
    // outbox.MessageOutbox<OrderPlaced>(m => m.EntityFrameworkOutbox(ef => ef.AddDbContext<AppDbContext>()));
});

opts.UseInbox(inbox =>
{
    inbox.EntityFrameworkInbox(ef => ef.AddDbContext<AppDbContext>());
    inbox.Options(o =>
    {
        o.ClaimDuration = TimeSpan.FromMinutes(5);      // must exceed ClaimRenewInterval
        o.ClaimRenewInterval = TimeSpan.FromMinutes(2);
    });
});
```

Both patterns also come with `InMemoryOutbox()` / `InMemoryInbox()` for tests. Concurrency is guarded by
fencing tokens (an EF Core concurrency token in the SQL stores), so a stalled instance can never overwrite
the work of the instance that took over. Expired records are removed by background workers
(`RetentionPeriod`, `CleanupInterval`).

Add a migration after mapping the entities — the tables are ordinary EF Core tables in your own database.

## State machines

`FxLink.StateMachine` models long-running business processes as state machines driven by messages.
Instances are persisted (EF Core), correlated to events by an id or a predicate, and can schedule
messages, send requests and publish events as they transition.

```csharp
public sealed class ReservationInstance : IStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string State { get; set; }
    public Guid OrderId { get; set; }
}

public sealed class ReservationStateMachine : StateMachine<ReservationInstance>
{
    public IState Reserved { get; private set; }
    public IEvent<ReserveInventory> ReserveInventoryEvent { get; private set; }
    public IEvent<ReleaseInventory> ReleaseInventoryEvent { get; private set; }

    public ReservationStateMachine()
    {
        Event(ReserveInventoryEvent, e => e.CorrelationId(x => x.Message.OrderId));
        Event(ReleaseInventoryEvent, e => e.CorrelationId(x => x.Message.OrderId));

        Initially(When(ReserveInventoryEvent)
            .Then(ctx => ctx.Instance.OrderId = ctx.Message.OrderId)
            .TransitionTo(Reserved));

        During(Reserved, When(ReleaseInventoryEvent)
            .Publish(ctx => new InventoryReleased { OrderId = ctx.Instance.OrderId })
            .Complete());
    }
}

opts.AddStateMachines(machines =>
{
    machines.Of<ReservationStateMachine>(cfg => cfg.EntityFrameworkRepository(ef =>
    {
        ef.DbContextFactory(sp => sp.GetRequiredService<AppDbContext>());
        ef.UseConcurrencyMode(mode => mode.Optimistic());   // or Pessimistic(SqlDialect.PostgreSql)
    }));
});
```

The DSL also covers conditionals (`If`/`IfElse`), schedules (`Schedule`/`Unschedule`), request steps with
completed/failed/timeout events, activities, and per-event handling of missing instances. The full surface
is exercised in [`tests/StateMachine`](tests/StateMachine).

## Routing slips

`FxLink.RoutingSlip` executes a sequence of activities and, if one fails, compensates the ones that already
completed in reverse order.

```csharp
public sealed class ReserveInventoryActivity : IExecuteActivity<ReserveInventoryArgs, ReserveInventoryLog>
{
    public Task<IExecuteResult<ReserveInventoryLog>> ExecuteAsync(
        IExecuteContext<ReserveInventoryArgs, ReserveInventoryLog> context, CancellationToken token = default) =>
        Task.FromResult(context.Completed(new ReserveInventoryLog { ReservationId = Guid.NewGuid() }));

    public Task<ICompensatedResult> CompensateAsync(
        ICompensateContext<ReserveInventoryLog> context, CancellationToken token = default) =>
        Task.FromResult(context.Compensated());   // undo using context.Log
}

opts.AddRoutingSlip(slip => slip
    .AddActivity<ReserveInventoryActivity>()
    .AddActivity<ChargePaymentActivity>());

// run it from a consumer
await executor.RunAsync(cfg => cfg
    .AddArgument(new ReserveInventoryArgs { Quantity = 1 })
    .AddArgument(new ChargePaymentArgs { Amount = 100 })
    .SetVariable("customerId", 123), token);
```

A working five-step order saga lives in [`tests/Order/TestRoutingSlip`](tests/Order/TestRoutingSlip).

## Building and testing

```bash
dotnet build FxLink.slnx
dotnet test tests/FxLink.Tests/FxLink.Tests.csproj
dotnet test tests/FxLink.Messaging.EntityFrameworkCore.Tests/FxLink.Messaging.EntityFrameworkCore.Tests.csproj
dotnet test tests/FxLink.Nats.Tests/FxLink.Nats.Tests.csproj
```

`global.json` pins the .NET 10 SDK; the libraries multi-target .NET 8, 9 and 10. The transport tests that
need a live broker are not part of the automated suites — the sample apps under `tests/` (`Order`,
`Payment`, `StateMachine`) are runnable end-to-end demos.

## Contributing

Issues and pull requests are welcome at <https://github.com/quyvu01/fx-link>.

## License

Apache License 2.0 — see [LICENSE](LICENSE).
