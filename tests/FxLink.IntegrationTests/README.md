# FxLink.IntegrationTests

Local-only suite that exercises real brokers instead of mocks (see
`docs/lessons-from-fxmap.md` section 4 for why: a mocked channel/connection can't reproduce what an
actual outage, restart, or long-poll does). **Not wired into CI** — see
`.github/workflows/build-publish.yml`'s `test` job, which runs the other test projects only. Every
test here checks broker reachability first and returns early (reported as a pass, not a failure)
when a broker isn't up, so running the whole solution's tests without any containers running is
safe — it just skips this project's coverage silently.

## Brokers this suite expects

| Transport | Host:port | Container (this dev machine) |
|---|---|---|
| RabbitMQ | `localhost:5672` (mgmt UI: 15672, guest/guest) | `local_rabbitmq` |
| NATS (JetStream) | `localhost:4222` | `vigorous_cartwright` (must run with `-js`) |
| SQS/SNS | `localhost:4567` | `fxlink_localstack_test` — **not** the shared `fxmap_localstack_test` on 4566, which belongs to a different project and has SNS disabled |
| Azure Service Bus | — | no working local emulator yet (see `docs/azure-service-bus-transport-design.md`); not covered here |

Recreate the LocalStack container if it's missing (SNS needs `SQS_ENDPOINT_STRATEGY=path` and
`LOCALSTACK_HOST` set to the real external port, or returned queue URLs point at the wrong
host/port and nothing a test publishes will ever be received):

```bash
podman run -d --name fxlink_localstack_test -p 4567:4566 \
  -e SERVICES=sns,sqs \
  -e DEFAULT_REGION=us-east-1 \
  -e LOCALSTACK_HOST=localhost:4567 \
  -e SQS_ENDPOINT_STRATEGY=path \
  docker.io/localstack/localstack:3.0
```

RabbitMQ and NATS are assumed to already be running as part of this machine's normal dev setup.

## Why no CI service containers (yet)

GitHub Actions can run these as job `services:` containers (standard Docker, no podman socket
quirks there), which would make the RabbitMQ/NATS/SQS tests run on every push. That's a reasonable
next step, deliberately not done in this change — scope was bug fixes plus a first integration
suite, not a CI infrastructure project. Until then, this suite only runs when someone (or a hook)
runs `dotnet test tests/FxLink.IntegrationTests/FxLink.IntegrationTests.csproj` locally.

## What's here

- `RabbitMqLifecycleTests`, `NatsLifecycleTests`, `SqsLifecycleTests` — start/request-or-publish/
  stop, three times in a row (docs/lessons-from-fxmap.md section 3). `SqsLifecycleTests` is the
  direct regression test for the `SqsClient` fix in this change (the duplicate queue-url list entry
  on every restart) — it reverts cleanly to failing (`Count` 3 instead of 1) against the pre-fix
  `SqsClient.cs`, confirmed by temporarily reverting it during this change.
- `RabbitMqOutageTests` — `BrokerProxy` cuts the real connection for 3s and restores it; a request
  succeeds again afterward. This is the FxMap bug the lessons doc is named after (section 2: a
  server-named reply queue renamed after `AutomaticRecoveryEnabled` reconnects) — FxLink's existing
  `QueueNameChangedAfterRecoveryAsync` handler is what makes this pass.
- `RabbitMqConcurrencyTests` — 200 concurrent requests, each must get back its own payload (section
  6: proves `ea.Body` isn't read after the handler that owns it returns).
- `RabbitMqLoadTests` — 3000 concurrent publishes, all consumed exactly once within a time bound
  (the user's own "load test" ask; no FxMap-doc precedent for this one).
- `BrokerProxy` (`Support/BrokerProxy.cs`) — a ~180-line TCP forwarder (`Cut()`/`Restore()`), written
  for this suite rather than copied from FxMap's version (that's in a separate repo this session
  didn't have access to).

## Known gaps

- Azure Service Bus: no test here, no working local broker (see the design doc).
- The "orphaned poller from a mid-setup exception" half of the SqsClient fix (see
  `docs/lessons-from-fxmap.md` section 3) isn't directly exercised — only the simpler "duplicate
  queue-url list entry on every restart" half is (`SqsLifecycleTests`), since forcing a mid-setup
  failure needs fault injection this suite doesn't have yet.
- Cancellation/timeout scenarios for `IRequester<T>` (section 5 — caller cancels while the owner is
  slow; owner slower than the timeout, late replies don't mix up with other requests) aren't
  implemented yet.
- NATS and SQS don't have their own outage (`BrokerProxy`) tests yet, only RabbitMQ does.
- Two quirks worth knowing if you extend this suite against the same local brokers: (1) LocalStack's
  SQS long-poll (`WaitTimeSeconds=20`) doesn't reliably wake early when a message arrives mid-wait
  the way real SQS does — budget noticeably more than 20s for anything that waits on SQS delivery;
  (2) a publish call returning without throwing does NOT prove the target exchange/topic exists yet
  for RabbitMQ or SQS — confirm readiness with a real round-trip (publish-then-wait-for-consumption),
  not just "the publish call didn't throw".
