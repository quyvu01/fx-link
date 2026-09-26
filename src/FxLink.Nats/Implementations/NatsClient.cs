using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Nats.Abstractions;
using FxLink.Nats.Constants;
using FxLink.Nats.Extensions;
using FxLink.Nats.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace FxLink.Nats.Implementations;

internal sealed class NatsClient(
    INatsConnectionProvider natsConnection,
    IMessageKeys messageKeys,
    IServiceProvider serviceProvider,
    INatsConfiguration natsConfiguration)
    : IMessageBrokerConnector, INatsMessagingClient
{
    private const string MessageTypeHeader = "MessageType";
    private const string ReplyToHeader = "ReplyTo";

    private readonly ILogger<NatsClient> _logger = serviceProvider.GetService<ILogger<NatsClient>>();

    // messageTypeName (AssemblyQualifiedName, from the "MessageType" header) -> the closed
    // IClientConnector<> type to resolve from DI — same caching role as RabbitMqClient's
    // _connectorTypeCache.
    private readonly ConcurrentDictionary<string, Type> _connectorTypeCache = new();

    // One subject per message type inside the single shared stream (the fan-out point, mirrors
    // RabbitMq's fanout exchange); one durable pull consumer per consumer type reads the subjects
    // it consumes.
    private readonly ConcurrentDictionary<Type, string> _messageNamesByType = new();

    private bool _supportsScheduling;
    private bool _connectionEventsHooked;

    // Unique per process instance and never shared: only this instance's requests point at it.
    public string ReplySubject { get; } = $"_INBOX.fxlink.{Guid.NewGuid():N}";

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await natsConnection.Connection.ConnectAsync();
        EnsureJetStreamAvailable();
        DetectServerCapabilities();
        HookConnectionEvents();

        var messageKeyMap = messageKeys.GetMessageKeys(); // messageType -> consumerTypes[]

        if (natsConfiguration.AutoProvision) await ProvisionStreamAsync(cancellationToken);

        // Invert messageType -> consumerTypes[] into consumerType -> messageTypes it consumes, so
        // each consumer only filters on the subjects it actually needs.
        var messageTypesByConsumerType = messageKeyMap
            .SelectMany(kv => kv.Value.Select(consumerType => (consumerType, messageType: kv.Key)))
            .GroupBy(x => x.consumerType, x => x.messageType);

        // Loops started here must die with this StartAsync call, not just with the host: the
        // supervisor re-invokes StartAsync to restart a faulted connector, and loops left over from
        // the previous run would otherwise keep consuming alongside the new ones.
        using var loopsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var fault = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loops = new List<Task>();

        try
        {
            foreach (var group in messageTypesByConsumerType)
            {
                var consumerType = group.Key;
                var consumerName = ResolveConsumerName(consumerType);
                var messageTypes = group.ToArray();

                if (natsConfiguration.AutoProvision)
                {
                    await ProvisionConsumerAsync(consumerName,
                        messageTypes.Select(GetSubject).ToArray(), cancellationToken);
                    await ProvisionConsumerAsync(consumerName.DeadLetterConsumerName(),
                        messageTypes.Select(GetDeadLetterSubject).ToArray(), cancellationToken);
                }
                else
                {
                    // Fail fast at startup if externally-managed topology is missing, rather than at
                    // the first receive.
                    await natsConnection.JetStream.GetConsumerAsync(natsConfiguration.StreamName, consumerName,
                        cancellationToken);
                }

                loops.Add(RunLoopAsync($"consumer {consumerName}",
                    ct => ConsumeAsync(consumerName, consumerType, ct), fault, loopsCts.Token));
            }

            loops.Add(RunLoopAsync("reply subscription", ReplyAsync, fault, loopsCts.Token));

            // Run until StopAsync/host shutdown cancels the token — same contract as
            // RabbitMqClient.StartAsync — or until a loop gives up and faults the connector.
            var cancelled = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(cancelled, fault.Task) == fault.Task)
                ExceptionDispatchInfo.Capture(await fault.Task).Throw();
            await cancelled;
        }
        finally
        {
            await loopsCts.CancelAsync();
            try
            {
                await Task.WhenAll(loops);
            }
            catch
            {
                // Loops only ever end by cancellation; nothing left to report.
            }
        }
    }

    // Runs body until cancelled, retrying after failures. Once it has failed
    // ConsumeFailureThreshold times in a row it stops retrying and reports the last exception through
    // `fault`, which faults the whole connector: an ordinary blip (network drop, server restart) is
    // survived by retrying, but a loop that can't get back on its feet means the topology itself is
    // probably gone, and only a restart re-provisions it.
    private async Task RunLoopAsync(string name, Func<CancellationToken, Task> body,
        TaskCompletionSource<Exception> fault, CancellationToken token)
    {
        var failures = 0;
        while (!token.IsCancellationRequested)
        {
            var attemptStartedAt = DateTime.UtcNow;
            try
            {
                await body(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The bodies run until something breaks, so a failure after a long healthy stretch
                // is a fresh incident, not the next in a streak — without this, unrelated blips
                // weeks apart would add up to the threshold.
                if (DateTime.UtcNow - attemptStartedAt > NatsConstants.ConsumeRetryDelay * 2) failures = 0;

                if (++failures >= NatsConstants.ConsumeFailureThreshold)
                {
                    _logger?.LogError(ex, "NATS {Loop} failed {Count} times in a row; faulting the connector",
                        name, failures);
                    fault.TrySetResult(ex);
                    return;
                }

                _logger?.LogWarning(ex, "NATS {Loop} failed ({Count}/{Max}); retrying", name, failures,
                    NatsConstants.ConsumeFailureThreshold);
                try
                {
                    await Task.Delay(NatsConstants.ConsumeRetryDelay, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    // NATS reconnects on its own, so a dropped connection isn't a failure the supervisor needs to
    // know about — these handlers are purely observational, same stance as RabbitMqClient's.
    private void HookConnectionEvents()
    {
        if (_connectionEventsHooked) return;
        _connectionEventsHooked = true;
        natsConnection.Connection.ConnectionDisconnected += (_, _) =>
        {
            _logger?.LogWarning("NATS connection lost; the client will reconnect automatically");
            return default;
        };
        natsConnection.Connection.ConnectionOpened += (_, _) =>
        {
            _logger?.LogInformation("NATS connection established");
            return default;
        };
    }

    // JetStream is opt-in on the server (nats-server -js / a jetstream {} config block). Without it
    // every stream/consumer request below gets "No responders" — true, but a poor pointer to the cause.
    private void EnsureJetStreamAvailable()
    {
        var info = natsConnection.Connection.ServerInfo;
        if (info is { JetStreamAvailable: false })
            throw new InvalidOperationException(
                $"The NATS server at {natsConnection.Connection.Opts.Url} (version {info.Version}) doesn't have " +
                "JetStream enabled, which FxLink.Nats requires. Start it with JetStream on (nats-server -js, or " +
                "`nats:latest -js` for the docker image).");
    }

    private void DetectServerCapabilities()
    {
        var version = natsConnection.Connection.ServerInfo?.Version;
        _supportsScheduling = Version.TryParse(version?.Split('-')[0], out var parsed) &&
                              parsed >= NatsConstants.MinSchedulingServerVersion;
        if (!_supportsScheduling)
            _logger?.LogWarning(
                "NATS server {Version} predates message scheduling (2.12): retries and delayed publishes will fail",
                version);
    }

    // A message type can override its subject via IMessageDefinition<TMessage>'s
    // MessageConfigurator.Name(...); a definition registered without a name (e.g. one that only
    // calls UseRawJsonSerializer()) falls back to the type-based default.
    private string ResolveMessageName(Type messageType)
    {
        var messageDefinition = serviceProvider.GetService(typeof(IMessageDefinition<>)
            .MakeGenericType(messageType)) as IMessageDefinition;
        var customName = messageDefinition?.MessageConfigurator.GetName();
        return customName is { Length: > 0 }
            ? NatsNamingExtensions.SanitizeSubject(customName)
            : messageType.GetSubjectName();
    }

    // A consumer can override its durable name via IConsumerConfigurator<TConsumer>.ReceivedEndpoint(...).
    private string ResolveConsumerName(Type consumerType)
    {
        var consumerConfiguration = serviceProvider
                .GetRequiredService(typeof(IConsumerConfiguratorResolver<>).MakeGenericType(consumerType)) as
            IConsumerConfiguratorResolver;
        var customName = consumerConfiguration!.Resolve<INatsReceiveEndpointDefinition>()?.ReceiveEndpoint;
        return customName is { Length: > 0 }
            ? NatsNamingExtensions.SanitizeConsumer(customName)
            : consumerType.GetConsumerName();
    }

    public string GetSubject(Type messageType) =>
        $"{natsConfiguration.SubjectPrefix}.{_messageNamesByType.GetOrAdd(messageType, ResolveMessageName)}";

    // "_dlq" can't collide with a message subject: it would need a message type living in a
    // namespace literally named "_dlq".
    public string GetDeadLetterSubject(Type messageType) =>
        $"{natsConfiguration.SubjectPrefix}._dlq.{_messageNamesByType.GetOrAdd(messageType, ResolveMessageName)}";

    // One stream captures every FxLink subject — message, dead-letter and scheduling-holder alike.
    // Created through the raw JetStream API because the typed StreamConfig has no way to enable
    // message schedules. Limits retention (bounded by MaxAge) rather than interest retention: a
    // scheduled message sits under a holding subject no consumer is interested in, and interest
    // retention would drop it on arrival, before it could ever fire.
    private async Task ProvisionStreamAsync(CancellationToken token)
    {
        var name = natsConfiguration.StreamName;
        var config = JsonSerializer.Serialize(new
        {
            name,
            subjects = new[] { $"{natsConfiguration.SubjectPrefix}.>" },
            retention = "limits",
            max_age = natsConfiguration.MaxAge.Ticks * 100L, // nanoseconds
            allow_msg_schedules = true,
            allow_msg_ttl = true
        });

        var created = await StreamApiAsync($"$JS.API.STREAM.CREATE.{name}", config, token);
        if (created.ErrorCode is null) return;
        if (created.ErrorCode != StreamNameInUse) throw created.ToException(name);

        var updated = await StreamApiAsync($"$JS.API.STREAM.UPDATE.{name}", config, token);
        if (updated.ErrorCode is not null) throw updated.ToException(name);
    }

    private const int StreamNameInUse = 10058;

    private async Task<StreamApiResult> StreamApiAsync(string apiSubject, string requestJson, CancellationToken token)
    {
        var reply = await natsConnection.Connection.RequestAsync(apiSubject, requestJson,
            requestSerializer: NatsUtf8PrimitivesSerializer<string>.Default,
            replySerializer: NatsUtf8PrimitivesSerializer<string>.Default, cancellationToken: token);
        using var document = JsonDocument.Parse(reply.Data ?? "{}");
        return document.RootElement.TryGetProperty("error", out var error)
            ? new StreamApiResult(error.GetProperty("err_code").GetInt32(),
                error.TryGetProperty("description", out var description) ? description.GetString() : null)
            : new StreamApiResult(null, null);
    }

    private readonly record struct StreamApiResult(int? ErrorCode, string Description)
    {
        public Exception ToException(string stream) =>
            new InvalidOperationException(
                $"Failed to provision NATS stream '{stream}': {Description} (code {ErrorCode}).");
    }

    private async Task ProvisionConsumerAsync(string consumerName, string[] filterSubjects, CancellationToken token) =>
        await natsConnection.JetStream.CreateOrUpdateConsumerAsync(natsConfiguration.StreamName,
            new ConsumerConfig(consumerName)
            {
                FilterSubjects = filterSubjects,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                // New, not All: a consumer only receives what's published after it exists, the same
                // as a queue that was only just bound to an exchange.
                DeliverPolicy = ConsumerConfigDeliverPolicy.New,
                MaxDeliver = natsConfiguration.MaxDeliver,
                AckWait = natsConfiguration.AckWait,
                MaxAckPending = natsConfiguration.MaxAckPending
            }, token);

    // Pull-based like SQS, but the client library does the polling: ConsumeAsync keeps a batch of
    // up to MaxMsgs messages buffered and refills it as they're acked, and handles heartbeats itself.
    private async Task ConsumeAsync(string consumerName, Type consumerType, CancellationToken token)
    {
        var consumer = await natsConnection.JetStream
            .GetConsumerAsync(natsConfiguration.StreamName, consumerName, token);
        
        var messagesAsync = consumer.ConsumeAsync(serializer: NatsUtf8PrimitivesSerializer<string>.Default,
            opts: new NatsJSConsumeOpts { MaxMsgs = natsConfiguration.MaxAckPending },
            cancellationToken: token);
        
        await foreach (var message in messagesAsync)
            // Not awaited: MaxAckPending already caps how many can be in flight, so handlers run
            // concurrently instead of head-of-line blocking behind one slow message.
            _ = ProcessAndAckAsync(message, consumerType, token);
    }

    // Every message on this instance's inbox is a response to one of its own outstanding requests,
    // dispatched via ProcessResponseMessageAsync instead of ProcessMessageReceivedAsync (no
    // consumerType, no IConsumerConnector<TMessage> — it completes an in-process await instead).
    private async Task ReplyAsync(CancellationToken token)
    {
        await foreach (var message in natsConnection.Connection.SubscribeAsync(ReplySubject,
                           serializer: NatsUtf8PrimitivesSerializer<string>.Default, cancellationToken: token))
        {
            try
            {
                if (message.Data is { } body && ResolveConnector(message.Headers) is { } connector)
                    await connector.ProcessResponseMessageAsync(body, token);
            }
            catch (Exception ex)
            {
                // Whoever was waiting on this response has its own timeout (Requester<TRequest>'s
                // CancelAfter), so there's nothing useful to retry — make it visible and move on.
                _logger?.LogCritical(ex, "Unhandled exception processing a response message");
            }
        }
    }

    private AbstractNatsConnector ResolveConnector(NatsHeaders headers)
    {
        if (headers is null || !headers.TryGetValue(MessageTypeHeader, out var values) ||
            values.ToString() is not { Length: > 0 } messageTypeName)
            return null;

        var connectorType = _connectorTypeCache.GetOrAdd(messageTypeName, static type =>
        {
            var msgType = Type.GetType(type);
            return msgType is null ? null : typeof(IClientConnector<>).MakeGenericType(msgType);
        });
        return connectorType is null ? null : (AbstractNatsConnector)serviceProvider.GetRequiredService(connectorType);
    }

    private async Task ProcessAndAckAsync(NatsJSMsg<string> message, Type consumerType, CancellationToken token)
    {
        try
        {
            if (message.Data is { } body && ResolveConnector(message.Headers) is { } connector)
            {
                var replyTo = message.Headers!.TryGetValue(ReplyToHeader, out var reply) ? reply.ToString() : null;
                await connector.ProcessMessageReceivedAsync(body, consumerType, replyTo, token);
            }
        }
        catch (Exception ex)
        {
            // RetryPipelineBehavior already owns retry/dead-letter handling and never rethrows in
            // the normal case — reaching here means that mechanism itself failed. Ack anyway to
            // avoid an uncontrolled redelivery loop; the failure is only visible via this log.
            _logger?.LogCritical(ex, "Unhandled exception escaped the consumer pipeline for subject {Subject}",
                message.Subject);
        }
        finally
        {
            // CancellationToken.None deliberately: a message that was already handled must still be
            // acked even if the caller's token was just cancelled, or it's redelivered on restart.
            try
            {
                await message.AckAsync(cancellationToken: CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to ack message on subject {Subject}", message.Subject);
            }
        }
    }

    public async Task PublishAsync(string subject, string messageBody, string messageTypeName, Guid? messageId = null,
        string replyTo = null, CancellationToken token = default)
    {
        var headers = new NatsHeaders
        {
            // Read back on receive to resolve which IClientConnector<TMessage> to dispatch into.
            [MessageTypeHeader] = messageTypeName
        };
        if (replyTo is { Length: > 0 }) headers[ReplyToHeader] = replyTo;

        var ack = await natsConnection.JetStream.PublishAsync(subject, messageBody,
            serializer: NatsUtf8PrimitivesSerializer<string>.Default,
            opts: messageId is { } id ? new NatsJSPubOpts { MsgId = id.ToString() } : null,
            headers: headers, cancellationToken: token);
        ack.EnsureSuccess();
    }

    public async Task PublishScheduledAsync(string targetSubject, string messageBody, string messageTypeName,
        TimeSpan delay, CancellationToken token = default)
    {
        if (!_supportsScheduling)
            throw new NotSupportedException(
                "Delayed delivery (retry backoff, delayed publish) needs NATS server 2.12 or newer for " +
                "message scheduling.");

        // Rounded up to whole seconds — the schedule header only carries second precision.
        var at = DateTime.UtcNow.Add(delay).AddSeconds(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var ack = await natsConnection.JetStream.PublishAsync(
            $"{natsConfiguration.SubjectPrefix}._sched.{Guid.NewGuid():N}", messageBody,
            serializer: NatsUtf8PrimitivesSerializer<string>.Default,
            headers: new NatsHeaders
            {
                ["Nats-Schedule"] = $"@at {at}",
                ["Nats-Schedule-Target"] = targetSubject,
                [MessageTypeHeader] = messageTypeName
            }, cancellationToken: token);
        ack.EnsureSuccess();
    }

    public async Task PublishReplyAsync(string replySubject, string messageBody, string messageTypeName,
        CancellationToken token = default) =>
        await natsConnection.Connection.PublishAsync(replySubject, messageBody,
            headers: new NatsHeaders { [MessageTypeHeader] = messageTypeName },
            serializer: NatsUtf8PrimitivesSerializer<string>.Default, cancellationToken: token);

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        // Nothing to tear down: the connection is owned (and disposed) by INatsConnectionProvider,
        // the loops end with the token StartAsync was given, and the durable consumers are meant to
        // outlive this instance — other instances of the same consumer type keep reading through them.
        return Task.CompletedTask;
    }
}