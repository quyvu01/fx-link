# Lessons from FxMap (commits `96be306` … `8c4cae8`): what to check in FxLink

FxLink and FxMap share a lot of structure (the supervisor of the connectors, the RabbitMQ / NATS / SQS / Service Bus transports,
the CI that publishes the packages). Between the commit `96be306` and `8c4cae8` of FxMap (versions 4.1.3 → 4.1.5), tests against real
brokers found bugs that a unit test would not have shown. This note says, for each change, **whether FxLink can have the same bug,
where to look, and how to prove it with a test**. Nothing here was changed in FxLink: it is a checklist.

The FxMap diffs can be read with `git -C ../FxMap show <hash>`.

## Summary

| FxMap commit | Change | In FxLink |
|---|---|---|
| `d45f9db` | Supervisor: a running server is reported `Stopped` after `OneForAll` restarts | **Same code** (`FxLink/Supervision/ServerSupervisor.cs:125-135`): fix |
| `d45f9db` | Supervisor: `RestForOne` restarts the wrong servers (order of a dictionary) | **Same code** (`ServerSupervisor.cs:14, 311`): fix |
| `d45f9db` | RabbitMQ: the reply queue was named by the broker; after a recovery the requests carry the old name | FxLink has a handler for the rename (`RabbitMqClient.cs:118-130`): **prove it with an outage test** |
| `a38814b` | `StopAsync` disposed what the next `StartAsync` needs (NATS, SQS, Kafka); started twice = duplicates (SQS, RabbitMQ) | No semaphore in the transports of FxLink; **check each connector's start/stop pair** |
| `96be306` | RabbitMQ: the body of a reply (`ea.Body`) is reused after the handler returns | `Body.Span` is read inside the handler (`RabbitMqClientConnector.cs:103, 156`): looks safe, **prove it with a concurrency test** |
| `f617ead` | Service Bus: the `ServiceBusClient` was never disposed (connection quota exceeded in CI) | `ServiceBusConnectionProvider` disposes it: OK. Check it is registered so the container disposes it |
| `8d9de7b` | Raised the lower bounds of dependencies with known vulnerabilities | **Run the vulnerability check** (see below) |
| `7f2020c` | SQS logged a normal shutdown as an error | FxLink uses `LogInformation` in all workers: OK |
| `abbf177` / `4d974bc` | CI: the Service Bus tests are required before the version is calculated and published | **Check the workflow of FxLink** |
| `675a6d5` | Kafka partitions / replication factor | Not applicable (no Kafka transport) |
| `df9b8ef` | HotChocolate: conditional rules of a dependency chain | Not applicable |
| `0d7945f`, `3c1da43` | Verification report, sample cleanup | Not applicable (idea: publish what FxLink is tested against) |

## 1. Supervisor: the cancelled run is taken for a failure (`OneForAll`, `RestForOne`)

**FxLink:** `src/FxLink/Supervision/ServerSupervisor.cs`, `StartServer` (lines 125-135):

```csharp
state.Cts = CancellationTokenSource.CreateLinkedTokenSource(...);
state.RunningTask = Task.Run(async () =>
{
    try { await state.Server.StartAsync(state.Cts.Token); }
    catch (OperationCanceledException) when (state.Cts.IsCancellationRequested) { /* cancelled */ }
    catch (Exception ex) { await HandleServerFailureAsync(state, ex); }
});
```

A restart of several servers cancels `state.Cts`, then `StartServer` **replaces** `state.Cts` with a new source while the old run is still
unwinding. The filter `state.Cts.IsCancellationRequested` then reads the **new** (not cancelled) source: the cancellation is taken for a
failure; the directive for `OperationCanceledException` is `Stop`, so the server that is running is marked `Stopped` for good (no restart, but
the health says it is dead). It depends on timing, so it shows up on some runs only (in FxMap 4 runs out of 6).

**Fix:** the run keeps its own source:

```csharp
var cts = CancellationTokenSource.CreateLinkedTokenSource(_supervisorCts?.Token ?? CancellationToken.None);
state.Cts = cts;
state.RunningTask = Task.Run(async () =>
{
    try { UpdateHealth(state, ServerHealth.Healthy); await state.Server.StartAsync(cts.Token); }
    catch (Exception) when (cts.IsCancellationRequested) { /* cancelled by a restart or by the stop */ }
    catch (Exception ex) { await HandleServerFailureAsync(state, ex); }
});
```

**Order of `RestForOne`** (`ServerSupervisor.cs:14, 309-311`): `_servers` is a `ConcurrentDictionary`, and `RestartServersFromAsync` does
`_servers.Values.ToList()` then `FindIndex`: the order of a dictionary is not the order of registration, so the servers "after" the failed one are
wrong. Keep the order of registration:

```csharp
private readonly List<string> _registrationOrder = [];                 // next to _servers
lock (_registrationOrder) _registrationOrder.Add(serverId);            // in RegisterServer, after TryAdd
List<SupervisedServerState> serverList;                                // in RestartServersFromAsync
lock (_registrationOrder) serverList = _registrationOrder.Select(id => _servers[id]).ToList();
```

**Tests to copy:** `test/FxMap.Tests/UnitTests/Supervision/ServerSupervisorTests.cs` (14 tests, fake servers, backoff of a few ms): restart and
healthy again, backoff doubles and stops at the maximum, limit of restarts, circuit opens/resets, directives, the three strategies, stop, dispose
with the circuit open, failures far apart. The two tests that fail without the fixes are `OneForAll_restarts_every_server_once_without_a_cascade`
(run the class several times: it is flaky before the fix) and `RestForOne_restarts_the_failed_server_and_the_ones_registered_after_it`.
Adapt the namespace and `IRequestServer` to the interface of FxLink's connectors.

**Not fixed, to know:** a server whose `StartAsync` **returns** without being cancelled is not restarted (in FxMap the start of RabbitMQ and SQS
returns once everything is set up, so a return cannot be taken for a failure). A consumer channel that dies while the connection is open is not
seen by the supervisor. FxLink's `Recycle<RecyclableChannel>` (reply channel) already covers this for the reply channel: check the consumer
channels of the other connectors too.

## 2. RabbitMQ: the reply queue after the connection is recovered

FxMap declared the reply queue **without a name** (the broker picks one). `AutomaticRecoveryEnabled` recovers the topology, but a server-named
queue comes back with **another name**, while the requests kept the old one in `ReplyTo`: the owner replied to a queue that no longer
existed, **every request timed out until the process restarted**. The test that found it cut the TCP connection between the service and
RabbitMQ for 3 s and then retried requests for 60 s: it never recovered. The fix was a client-chosen name:

```csharp
_replyQueueName = $"fxmap-reply-{Guid.NewGuid():N}";
await _replyChannel.QueueDeclareAsync(_replyQueueName, durable: false, exclusive: true, autoDelete: true, arguments: null, cancellationToken: ct);
```

**FxLink** (`src/FxLink.RabbitMq/Implementations/RabbitMqClient.cs`) declares the reply queue with an empty name in a `Recycle`d channel
(line 143) and updates `ReplyQueueName` in a `QueueNameChangedAfterRecoveryAsync` handler (lines 118-130). That can be right, but nothing
shows it. Two things to prove with a test (the proxy below makes it simple):

1. after the connection is cut and restored, a request works again within about the recovery interval (FxMap: ~10 s);
2. the case where the **channel** is recycled (not the connection): `ReplyQueueName = replyQueue.QueueName` gets the new name, but requests
   already published carry the old one. Also check `autoDelete: false` with `exclusive: true` (line 143): the queue goes away with the connection.

If the test fails, the simplest fix is the client-chosen name above.

## 3. A server that is stopped and started again

The supervisor stops and starts again the servers with `OneForAll` and `RestForOne` (and a host can restart its worker). In FxMap `StopAsync` of
the NATS, SQS and Kafka servers **disposed** the semaphore (and the Kafka consumer) that the next `StartAsync` uses: `ObjectDisposedException`.
SQS also added the queue URLs to a list on every start (two pollers on the same queue), and RabbitMQ left the consumers of a start that had
failed half way on their channels. Rules that came out of it:

- `StopAsync` disposes only what `StartAsync` creates again; state shared between runs (semaphores, caches) is not disposed there
  (`SemaphoreSlim` without `AvailableWaitHandle` holds no resource).
- A resource that a run needs and that is closed on stop (Kafka consumer) is created **in** `StartAsync`.
- `StartAsync` begins by clearing what a previous run left (lists of queues, consumers, channels).

**FxLink:** search the `Stop*`/`Dispose*` of each connector/server (`grep -rn "StopAsync\|DisposeAsync" src/FxLink.*`), for example
`ServiceBusTransportClient.cs:107, 120` (`processor.DisposeAsync()`): is the processor created again when the connector is started again?

**Test to copy:** `test/FxMap.Tests/TransportTests/ServerLifecycleTests.cs`, `*_a_server_can_be_stopped_and_started_again`: build the owner
**without starting its host**, take the server from the container, then three times: `StartAsync(cts.Token)` in a task, check that a request is
answered, cancel, `StopAsync()`. It fails at round 2 when something is disposed (mutation-checked: putting the `Dispose()` back makes it fail).

## 4. Tests that cut the connection of a real broker (`BrokerProxy`)

The technique that found the RabbitMQ bug needs no control of the container (works on CI): a small TCP forwarder between the service and the broker.
Point the service to `127.0.0.1:<proxy port>`; `Cut()` closes every connection and refuses new ones; `Restore()` listens again on the same
port. Source: `test/FxMap.Tests/TransportTests/BrokerProxy.cs` in FxMap (about 110 lines, no dependency). Scenarios in
`BrokerOutageTests.cs` (one set per transport):

| Scenario | Expected |
|---|---|
| owner behind the proxy: cut 3 s, restore | the owner listens again; a retried request works within the limit |
| consumer behind the proxy: cut, send a request | it **fails within the timeout** (does not hang); works again after the restore |
| a request is in flight (the owner takes 2 s), cut at 0.5 s | it ends within the timeout; works after the restore |

Results in FxMap: NATS and SQS recovered (2-5 s); RabbitMQ owner 7 s, RabbitMQ consumer **never** (before the fix) / 11 s (after). The Kafka broker
advertises its own address, so a proxy cannot cut it. A SQS test must dispose the services **before** the proxies (otherwise the SDK retries
a refused connection and the shutdown takes 30+ s).

## 5. Cancellation and timeouts through the transport

`ServerLifecycleTests.cs`, in `LifecycleScenarios` (these passed on every FxMap transport; worth having for the FxLink requester `IRequester<T>`):

- the caller cancels a request while the owner is slow (5 s): the caller gets a cancellation **at once** (not at the end of the timeout), and the
  late reply of the owner does not break the next request;
- the owner is slower than the timeout: the request fails at the timeout, and after the late reply arrives, a request for another id returns **its
  own** answer (the answers are not mixed up).

## 6. RabbitMQ: `ea.Body` is reused (commit `96be306`)

In `AsyncEventingBasicConsumer.ReceivedAsync` the memory of `ea.Body` belongs to the consumer and is reused when the handler returns. FxMap
completed a `TaskCompletionSource` with the `ReadOnlyMemory` and read it **later** in the caller: under load it read the bytes of another
message. Fix: `tcs.TrySetResult(ea.Body.ToArray())`. A concurrency scenario (200 requests at once, half for each of two ids, each must get its
own answer) found it, and is now run for every transport.

**FxLink:** `RabbitMqClientConnector.cs:103, 156` reads `args.Body.Span` inside `ProcessResponseMessageAsync(ea)` that is awaited by the
handler, so the memory is still valid. Check that no path keeps `args.Body` (or a `ReadOnlyMemory` taken from it) after the handler returns,
including `RabbitMqScheduleExchangeProvider` and the retry/dead-letter paths. The test: many concurrent requests/messages with different
payloads, assert each result.

## 7. Dependencies with known vulnerabilities (commit `8d9de7b`)

FxMap raised the **lower bound** of three packages because the lowest allowed version pulled a vulnerable one (a consumer that restores with
the lowest version gets it): `MongoDB.Driver` ≥ 3.9.0 (SharpCompress, Snappier), `HotChocolate.AspNetCore` ≥ 15.1.14 (critical stack overflow in
`HotChocolate.Language`), `Microsoft.Extensions.Caching.Memory` ≥ 8.0.1. For FxLink:

```bash
dotnet restore
dotnet list package --vulnerable --include-transitive      # every project; also --framework net8.0
```

then raise the `[x.y.z, )` bounds in the `.csproj` files of the libraries (not only the versions in the tests/samples). In FxMap the check is
clean for the 11 libraries, and the build has 0 warnings. Consider a CI step that fails on a vulnerable package.

## 8. CI

- **Service Bus tests before publishing** (`abbf177`): the emulator job was optional; it is now part of `transport-tests` and `calc-version`
  waits for it. The emulator listens on `5673` there because RabbitMQ has `5672`. Check that FxLink's workflow does not publish when a
  transport test is skipped or not required.
- **Service Bus emulator**: the connection quota of the emulator is limited; a leaked `ServiceBusClient` in tests ended with
  `ConnectionsQuotaExceeded` after a few tests (the FxMap fix `f617ead` disposes the client with the container; FxLink's
  `ServiceBusConnectionProvider` already has `DisposeAsync`, make sure the tests dispose their containers).
- A published version cannot be changed: fixes after a release go to the next patch (FxMap: 4.1.4 was released, these fixes are 4.1.5).

## Suggested order for FxLink

1. Supervisor: the two fixes and the 14 tests (fast, no broker).
2. `dotnet list package --vulnerable --include-transitive`.
3. RabbitMQ outage test with the proxy (the reply queue and the consumer channels).
4. Stop/start tests of each connector, then outage tests for NATS, SQS, Service Bus.
5. Cancellation / timeout / concurrency scenarios.
