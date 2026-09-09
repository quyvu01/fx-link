# FxLink vs. MassTransit — Feature Gap Analysis

Date: 2026-08-24
Scope: Full feature comparison against MassTransit. Not an implementation design — this is a prioritized inventory of what's missing.

## Understanding Summary

- FxLink is a home-grown messaging/CQRS-adjacent library modeled on MassTransit's architecture: consumer/publisher pipelines, RabbitMq transport, Saga-equivalent (`FxLink.StateMachine`), Courier-equivalent (`FxLink.RoutingSlip`), batch consumers, delayed messaging, and an Erlang/OTP-style supervision layer.
- Transport today is RabbitMq-only, but multi-transport (Kafka, Azure Service Bus, SQS, ...) is on the roadmap, so "only one transport" counts as a real gap here, not an intentional constraint.
- The hypothesis going in was "we're probably missing Job Consumers" — confirmed: no `IJobConsumer`, job saga, or concurrency/slot limiting exists anywhere in the codebase.
- Non-goal for this pass: no implementation design for any of these items, no code-quality review of what already exists.

## What FxLink Already Has

| Area | MassTransit equivalent | FxLink implementation |
|---|---|---|
| Send/Publish/Consume | `IBus`, `IPublishEndpoint`, `IConsumer<T>` | `IBus`, `IPublisher`, `IConsumer` |
| Request/Response | `IRequestClient<T>` | `IRequester` |
| Pipeline middleware | Filters/Pipes | `IConsumerPipelineBehavior`, `IPublisherPipelineBehavior` |
| Retry | `UseMessageRetry` | `RetryPipelineBehavior` (interval-based, ignore-exceptions, dead-letter) |
| Batch consumers | `IConsumer<Batch<T>>` | `IBatch<T>`, `IBatchAccumulator`, `BatchRetryPipelineBehavior` |
| Saga / state machine | Automatonymous / `MassTransitStateMachine<T>` | `FxLink.StateMachine` (+ EF Core persistence) |
| Courier (routing slips) | `IRoutingSlipExecutor` | `FxLink.RoutingSlip` (execute + compensate) |
| Delayed messaging | Delayed exchange / Quartz | `IDelayMessageProvider` + `RabbitMqScheduleExchangeProvider` |
| Faults | `Fault<T>` | `Fault.cs`, `RequestTimeoutExpired.cs` |
| Consumer definitions | `ConsumerDefinition<T>` | `IConsumerDefinition<T>` |
| Process supervision | — (no direct MT equivalent) | `ServerSupervisor` with OneForOne/OneForAll/RestForOne strategies — arguably **more sophisticated** than MassTransit here |

## Gap Table (priority order)

| # | Feature | MassTransit has | FxLink has | Priority | Why |
|---|---|---|---|---|---|
| 1 | **Job Consumers** | `IJobConsumer<T>`, job saga state machine, `ConcurrentJobLimit`, job retry/status tracking, `IJobService` submit/cancel/query | Nothing | **High** | Confirmed gap — no way to run long-lived, cancellable, concurrency-bounded background jobs with progress/status tracking. This was your stated suspicion. |
| 2 | **Concurrency / prefetch limits per consumer** | `UseConcurrencyLimit`, `PrefetchCount` on receive endpoint | Not found in `ConsumerConfigurator` or `AddRabbitMq` | **High** | Without this, a slow/expensive consumer can't be throttled independently of broker prefetch defaults — real production risk. |
| 3 | **Outbox pattern** | In-memory outbox + EF Core outbox (`AddEntityFrameworkOutbox`) for exactly-transactional publish | Not found | **High** | Without an outbox, publish-after-DB-write isn't atomic — a classic dual-write bug source in exactly the EF Core-backed services FxLink targets (`FlowX.EntityFrameworkCore`, `FxLink.StateMachine.EntityFrameworkCore`). |
| 4 | **Multi-transport support** | RabbitMq, Azure Service Bus, Amazon SQS, Kafka (rider), ActiveMQ, gRPC | RabbitMq only | **High** (per your stated roadmap) | You confirmed multi-transport is planned; today `IMessageBrokerConnector` has exactly one implementation, so this is architecture debt, not just a missing feature. |
| 5 | **Test harness** | `ITestHarness` / `InMemoryTestHarness` with `Consumed`, `Published`, `Sent` assertion helpers | `InMemory` transport exists but no assertion/harness API | **Medium-High** | Testability of consumers today likely means hand-rolled fakes; a harness is what makes consumer unit tests fast to write and keeps them from rotting. |
| 6 | **Observability (OpenTelemetry / diagnostics)** | Built-in `ActivitySource`, metrics, `MassTransit.Diagnostics` | No `ActivitySource`/diagnostics found | **Medium-High** | No distributed tracing across publish→consume hops or broker health metrics out of the box — hard to debug in production without it. |
| 7 | **Recurring / scheduled messages (cron)** | Quartz.NET integration (`AddQuartzConsumers`, recurring schedules) | Only one-shot delay via RabbitMq delayed-exchange | **Medium** | `IDelayMessageProvider` covers "send later once"; there's no equivalent of a recurring cron-style scheduled message. |
| 8 | **Message topology / conventions** | Exchange/queue naming conventions, `IEntityNameFormatter`, endpoint name formatters | Not evident — no dedicated topology/convention layer found | **Medium** | Affects how predictable/overridable exchange & queue names are across a large service fleet; matters more as multi-transport lands (#4). |
| 9 | **Composite/correlated saga events** | Composite events, `CorrelateById`/`CorrelateBy` fluent helpers, saga event correlation edge cases (missing instance policies) | Basic event/state model in `FxLink.StateMachine`; no composite-event operator found | **Medium** | Fine for simple sagas; will bite once state machines need to wait on N of M events or handle out-of-order/missing-instance cases. |
| 10 | **Saga persistence providers beyond EF Core** | EF Core, MongoDB, Redis, in-memory, NHibernate | EF Core + in-memory only | **Low-Medium** | Only matters if a consuming service isn't already on EF Core/relational storage. |
| 11 | **Circuit breaker / rate limiter middleware** | `UseCircuitBreaker`, `UseRateLimit` filters | `ServerSupervisor` handles process-level restart strategies, but no per-consumer circuit-breaker/rate-limit filter | **Low-Medium** | Supervision covers "restart a dead worker"; it doesn't cover "stop calling a failing downstream dependency for N seconds," which is a different failure mode. |
| 12 | **Kafka Rider / topic-specific producer API** | `AddRider().AddProducer/AddConsumer` for Kafka | N/A (no Kafka transport yet) | **Low** (tracks with #4) | Only relevant once Kafka transport work starts. |

## Non-Goals / Deliberate Non-Parity

- **Erlang/OTP-style supervision** — FxLink's `ServerSupervisor` has no MassTransit equivalent and is arguably a differentiator, not a gap. Worth keeping and documenting as a selling point rather than "catching up."
- Full API-surface parity with MassTransit is not the goal — this list is meant to identify *missing capability*, not missing method names.

## Assumptions Made

- Priority ranking is based on (a) how commonly the feature is relied on in typical MassTransit-based production systems, and (b) blast radius if missing (data-consistency/production-risk items ranked above nice-to-haves). No other prioritization criteria were given, so treat the ranking as a starting proposal, not a final backlog order.
- "Confirmed missing" claims are based on repo-wide grep across `src/` as of commit `1afacad` (branch `dev`, 2026-08-24) — not a guarantee the underlying capability isn't half-built somewhere not yet visible via naming.

## Decision Log

| Decision | Alternatives considered | Why chosen |
|---|---|---|
| Deliver a gap-analysis report only, no implementation design | (a) Report + Job Consumer design in same session, (b) Job Consumer design only | User explicitly chose report-only for this session |
| Compare against MassTransit's full feature set | Narrow to consumer-side features only; narrow to Job Consumer only | User chose full comparison |
| Treat multi-transport as a real gap (not a non-goal) | Treat RabbitMq-only as intentional scope | User confirmed multi-transport is on the roadmap |

## Suggested Next Step

Given the confirmed priority-1 status of **Job Consumers**, the natural next brainstorming session is a design pass for that feature specifically (job saga shape, concurrency/slot limiting, persistence, cancellation) — happy to start that whenever you're ready.
