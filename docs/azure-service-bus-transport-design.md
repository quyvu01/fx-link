# FxLink.Azure.ServiceBus — transport design

Status: agreed design, implemented, **unverified against a real broker**. Unit tests (configuration, naming,
the `Config.json` export) pass. The integration scenarios in the Testing section below (targeted retry, native
dead-letter, delay, request/response over a session, self-healing) have not been run — the Service Bus emulator
under podman couldn't be started (the emulator + its SQL Server dependency needs more RAM than was free on the
test machine). Unlike RabbitMQ, SQS and NATS, this transport has only been checked by the compiler and unit
tests so far.

## Understanding summary

- **What:** a fourth FxLink transport, `FxLink.Azure.ServiceBus`, plugged into the same
  `IMessageBrokerConnector` / `IClientConnector<>` / `IWireResultDispatcher` seams as RabbitMQ, SNS/SQS and NATS.
- **Model:** one **topic** per message type, one **subscription** per consumer type on each topic it consumes.
  Fan-out is every subscription receiving a copy. No sessions on the message path.
- **Receive:** `ServiceBusProcessor` in peek-lock mode; the SDK renews the lock.
- **Features:** publish, consume, request/response, retry backoff, dead-letter, delayed publish — the last three
  use native Service Bus features.
- **Why:** give Azure users a natural transport instead of wrapping RabbitMQ or SQS.
- **Non-goals:** sessions on the message path, queue-only mode, custom filters/rules, geo-replication, any change to
  the core package.

## Non-functional assumptions

- At-least-once delivery. Redelivery after a lost lock is expected; the Inbox is the de-duplication tool.
- Auth: connection string, or fully-qualified namespace + `DefaultAzureCredential` (Entra ID) as the primary path.
- Throughput knobs (concurrency, prefetch) are configurable; no premature tuning.
- `AutoProvision` on by default, switchable off — many organisations grant Send/Listen but not Manage.
- Naming, layout, custom naming (`MessageDefinition`, `ReceivedEndpoint`) and DI follow the existing transports.

## Design

### Topology

- Topic per message type, named `namespace.typename` (sanitised to Service Bus rules), or the custom name from
  `IMessageDefinition`. **No topic prefix** — unlike NATS there is no shared subject space to delimit, and RabbitMQ/SQS
  have none either.
- Subscription per consumer type, named from the consumer type or `ReceivedEndpoint(...)`. Subscription names are
  limited to 50 characters: longer names are truncated and suffixed with a stable hash of the full name.
- Each subscription has **exactly one rule**, replacing the default match-all rule:
  `[x-target] IS NULL OR [x-target] = '<subscription>'`. Ordinary messages carry no `x-target` and fan out to every
  subscription; a retry carries it and reaches only the subscription that failed. The rule is mandatory.
- Subscriptions are provisioned with the configured `MaxDeliveryCount`, `LockDuration` and native dead-lettering.
- One shared, session-enabled reply queue (default `fxlink-replies`) for request/response.
- A single topology model, built from the registered consumers, feeds both `AutoProvision` and the emulator
  `Config.json` export, so they cannot drift apart.

### Data flow

- **Publish:** cached `ServiceBusSender` per topic; JSON envelope body; application property `MessageType`
  (assembly-qualified name). `SendMessageAsync` completes once the broker has accepted the message. Only a plain
  publish sets `MessageId` to the context's `MessageId`; retries use a fresh id, because topic duplicate detection
  would otherwise silently drop them (same trap as NATS dedup ids).
- **Consume:** one `ServiceBusProcessor` per (topic, subscription), `AutoCompleteMessages = false`. The handler
  resolves the connector from `MessageType`, builds the `ConsumeContext` (including `ReplyTo`) and runs the pipeline,
  then completes the message. If an exception escapes the pipeline (the retry mechanism itself failed) the delivery is
  **abandoned**, so it is redelivered until `MaxDeliveryCount` and then lands in the native DLQ with a reason —
  unlike SQS/NATS, the loop already has a bound.
- **Delivery scope:** an `AsyncLocal` holds the current delivery (subscription, `ProcessMessageEventArgs`, a
  "dead-lettered" flag) while the pipeline runs; retry and dead-letter need it.
- **Retry:** the connector republishes to the same topic with `ScheduledEnqueueTime = now + delay`,
  `x-target = <current subscription>` and a new `MessageId`; the original delivery then completes normally.
- **Dead-letter:** `DeadLetterMessageAsync` on the current delivery, reason taken from the exception headers the
  pipeline already set; the completion step is skipped. No copy is sent — the DLQ holds the original message.
- **Delay:** publish with `ScheduledEnqueueTime` and no `x-target` (still fans out). Registered through
  `UseServiceBusDelayScheduler()` like the other transports, although it is native and has no ceiling.

### Request/response

SQS creates a reply queue per instance at run time; that needs Manage rights and the emulator cannot do it. Service
Bus's native pattern needs no new entity: one shared reply queue with sessions enabled, and each instance accepts a
session named by its own unique id.

- The request carries `ReplyTo = <reply queue>` and `ReplyToSessionId = <instance id>`.
- The responder sends the response to that queue with `SessionId = ReplyToSessionId`.
- An instance only accepts its own session, so a response can never reach another instance; only Send/Listen rights
  are needed. The result is handed to the core `IWireResultDispatcher`.
- This is the only use of sessions; the message path stays session-free.

### Health

`ServiceBusProcessor` retries transient failures itself; those are only logged. Unrecoverable errors
(`MessagingEntityNotFound`, `Unauthorized`) are counted, and three in a row fault the connector so the supervisor
restarts it and re-provisions the topology — the same approach as NATS.

### Configuration (`AddServiceBus`)

- Connection: `ConnectionString(...)`, or `Namespace("x.servicebus.windows.net")` with `DefaultAzureCredential`
  (default) or a supplied `TokenCredential`. `UseDevelopmentEmulator=true` in the connection string targets the emulator.
- Topology: `AutoProvision` (default true), `ReplyQueueName` (default `fxlink-replies`).
- Operations: `MaxDeliveryCount` (5), `LockDuration`, `MaxConcurrentCalls`, `PrefetchCount`,
  `MaxAutoLockRenewalDuration`.
- Custom names: `MessageDefinition.Name(...)` for topics, `ReceivedEndpoint(...)` for subscriptions.

### Testing

- **Unit tests** in the repo (no broker): configuration validation, naming (valid characters, 50-character truncation
  with a stable hash, no collisions), rule expression (quote escaping), and the emulator `Config.json` output.
- **Integration verification** against the Service Bus emulator under podman, from a harness outside the repo:
  publish/consume, targeted retry (other subscriptions must not receive it), dead-letter (message in the DLQ with a
  reason), delay, request/response over the session, and self-healing after the topic is deleted.
- The emulator cannot manage entities at run time, so `AutoProvision` is off there and the topology comes from the
  generated `Config.json`.

## Decision log

| # | Decision | Alternatives | Why |
|---|---|---|---|
| 1 | Topic + subscription, no sessions on the message path | Sessions everywhere; queue only | Keeps fan-out (queue-only loses it) without the receive-model change sessions bring |
| 2 | Native DLQ for dead-letter | Publish to a separate dead-letter topic | Keeps the original message with a reason, which is what Azure tooling inspects and resubmits |
| 3 | Targeted retry via `x-target` property + mandatory subscription rule | Retry to the topic (RabbitMQ parity); a retry queue per consumer | Parity re-runs every other subscription's consumer; per-consumer queues double the entities |
| 4 | Emulator: `AutoProvision` off, generate `Config.json` from one shared topology model | Hand-written config | The emulator cannot manage entities at run time; a shared model prevents drift |
| 5 | Reply via one shared session-enabled queue, one session id per instance | Per-instance temporary queue (SQS style) | No run-time entity creation, works with Send/Listen only and on the emulator |
| 6 | No `TopicPrefix` option | Prefix as in NATS | It delimits a shared subject space in NATS; here topics are independent entities, and other transports have none (YAGNI) |
| 7 | Abandon on an escaped exception | Complete anyway (SQS/NATS behaviour) | The delivery count bounds redelivery and ends in the DLQ with a reason |

## Risks

- The emulator needs a SQL Server container, which may run slowly or unreliably on Apple Silicon, and its AMQP port
  (5672) clashes with the local RabbitMQ container, so it has to be remapped.
- Emulator support for SQL rules, sessions and scheduled messages is unconfirmed; any gap will be reported as
  unverified rather than hidden.
- Peek-lock: work longer than `MaxAutoLockRenewalDuration`, or a lost connection, causes redelivery — the Inbox
  covers this.
