using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Azure.Messaging.ServiceBus;
using FxLink.Abstractions;
using FxLink.Azure.ServiceBus.Abstractions;
using FxLink.Azure.ServiceBus.Constants;
using FxLink.Azure.ServiceBus.Registries;
using FxLink.Azure.ServiceBus.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.Azure.ServiceBus.Implementations;

internal sealed class ServiceBusTransportClient(
    IServiceBusConnectionProvider connection,
    ServiceBusTopologyBuilder topologyBuilder,
    ServiceBusProvisioner provisioner,
    IServiceProvider serviceProvider,
    IServiceBusConfiguration configuration)
    : IMessageBrokerConnector, IServiceBusMessagingClient
{
    private readonly ILogger<ServiceBusTransportClient> _logger =
        serviceProvider.GetService<ILogger<ServiceBusTransportClient>>();

    // messageTypeName (AssemblyQualifiedName, from the "MessageType" property) -> the closed IClientConnector<> type
    // to resolve from DI — same caching role as RabbitMqClient's _connectorTypeCache.
    private readonly ConcurrentDictionary<string, Type> _connectorTypeCache = new();

    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
    private readonly ConcurrentDictionary<Type, string> _topicNamesByMessageType = new();

    private int _fatalErrors;

    public string ReplyQueueName => configuration.ReplyQueueName;

    // Unique per process instance and never shared: only this instance's requests name it, and only this instance
    // accepts the session.
    public string ReplySessionId { get; } = Guid.NewGuid().ToString("N");

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var topology = topologyBuilder.Build();
        if (configuration.AutoProvision)
            await provisioner.ProvisionAsync(topology, cancellationToken);

        var fault = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        _fatalErrors = 0;
        var processors = new List<ServiceBusProcessor>();
        ServiceBusSessionProcessor replyProcessor = null;

        try
        {
            foreach (var topic in topology.Topics)
            foreach (var subscription in topic.Subscriptions)
            {
                var processor = connection.Client.CreateProcessor(topic.Name, subscription.Name,
                    new ServiceBusProcessorOptions
                    {
                        // Settled explicitly after the pipeline — see HandleAsync.
                        AutoCompleteMessages = false,
                        ReceiveMode = ServiceBusReceiveMode.PeekLock,
                        MaxConcurrentCalls = configuration.MaxConcurrentCalls,
                        PrefetchCount = configuration.PrefetchCount,
                        MaxAutoLockRenewalDuration = configuration.MaxAutoLockRenewalDuration
                    });
                processor.ProcessMessageAsync += args => HandleAsync(args, subscription.Name, subscription.ConsumerType);
                processor.ProcessErrorAsync += args => OnErrorAsync(args, fault);
                processors.Add(processor);
                await processor.StartProcessingAsync(cancellationToken);
            }

            // Only this instance's session, so a response can never be received by another instance.
            replyProcessor = connection.Client.CreateSessionProcessor(configuration.ReplyQueueName,
                new ServiceBusSessionProcessorOptions
                {
                    AutoCompleteMessages = false,
                    SessionIds = { ReplySessionId },
                    MaxConcurrentSessions = 1,
                    MaxConcurrentCallsPerSession = configuration.MaxConcurrentCalls,
                    MaxAutoLockRenewalDuration = configuration.MaxAutoLockRenewalDuration
                });
            replyProcessor.ProcessMessageAsync += HandleReplyAsync;
            replyProcessor.ProcessErrorAsync += args => OnErrorAsync(args, fault);
            await replyProcessor.StartProcessingAsync(cancellationToken);

            // Run until StopAsync/host shutdown cancels the token — same contract as RabbitMqClient.StartAsync — or
            // until unrecoverable errors fault the connector.
            var cancelled = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(cancelled, fault.Task) == fault.Task)
                ExceptionDispatchInfo.Capture(await fault.Task).Throw();
            await cancelled;
        }
        finally
        {
            // Stopped here, not in StopAsync: the supervisor restarts by calling StartAsync again, and processors
            // left over from this run would otherwise keep consuming alongside the new ones.
            foreach (var processor in processors) await StopAsync(processor);
            if (replyProcessor is not null) await StopAsync(replyProcessor);
        }
    }

    private async Task StopAsync(ServiceBusProcessor processor)
    {
        try
        {
            await processor.StopProcessingAsync(CancellationToken.None);
            await processor.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to stop a Service Bus processor cleanly");
        }
    }

    private async Task StopAsync(ServiceBusSessionProcessor processor)
    {
        try
        {
            await processor.StopProcessingAsync(CancellationToken.None);
            await processor.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to stop the Service Bus reply processor cleanly");
        }
    }

    // The SDK retries transient failures itself, so those are only logged. What can't heal without a restart — the
    // entity was deleted or disabled, access was revoked — is counted, and enough of them in a row fault the
    // connector so the supervisor restarts it and re-provisions the topology.
    private Task OnErrorAsync(ProcessErrorEventArgs args, TaskCompletionSource<Exception> fault)
    {
        var fatal = args.Exception is ServiceBusException
        {
            Reason: ServiceBusFailureReason.MessagingEntityNotFound or ServiceBusFailureReason.MessagingEntityDisabled
        } or UnauthorizedAccessException;

        if (!fatal)
        {
            _logger?.LogWarning(args.Exception, "Service Bus processor error ({Source}) on {Entity}",
                args.ErrorSource, args.EntityPath);
            return Task.CompletedTask;
        }

        if (Interlocked.Increment(ref _fatalErrors) >= ServiceBusConstants.FatalErrorThreshold)
        {
            _logger?.LogError(args.Exception,
                "Service Bus {Entity} failed {Count} times in a row; faulting the connector", args.EntityPath,
                _fatalErrors);
            fault.TrySetResult(args.Exception);
        }
        else
        {
            _logger?.LogWarning(args.Exception, "Service Bus processor error ({Source}) on {Entity}", args.ErrorSource,
                args.EntityPath);
        }

        return Task.CompletedTask;
    }

    private AbstractServiceBusConnector ResolveConnector(ServiceBusReceivedMessage message)
    {
        if (!message.ApplicationProperties.TryGetValue(ServiceBusConstants.MessageTypeProperty, out var value) ||
            value?.ToString() is not { Length: > 0 } messageTypeName)
            return null;

        var connectorType = _connectorTypeCache.GetOrAdd(messageTypeName, static type =>
        {
            var msgType = Type.GetType(type);
            return msgType is null ? null : typeof(IClientConnector<>).MakeGenericType(msgType);
        });
        return connectorType is null
            ? null
            : (AbstractServiceBusConnector)serviceProvider.GetRequiredService(connectorType);
    }

    private async Task HandleAsync(ProcessMessageEventArgs args, string subscription, Type consumerType)
    {
        var connector = ResolveConnector(args.Message);
        if (connector is null)
        {
            // Nothing here can process it, and redelivering won't change that — park it, with the reason, instead
            // of dropping it silently.
            await args.DeadLetterMessageAsync(args.Message, "UnknownMessageType",
                "The message has no resolvable MessageType property.", CancellationToken.None);
            return;
        }

        var scope = new DeliveryScope(subscription, args);
        DeliveryScope.Current = scope;
        try
        {
            await connector.ProcessMessageReceivedAsync(args.Message.Body.ToString(), consumerType,
                args.Message.ReplyTo, args.Message.ReplyToSessionId, args.CancellationToken);

            // A dead-lettered delivery is already settled.
            if (!scope.DeadLettered)
                // CancellationToken.None deliberately: a message that was already handled must still be completed
                // even if the host is shutting down, or it's redelivered on restart.
                await args.CompleteMessageAsync(args.Message, CancellationToken.None);
            Interlocked.Exchange(ref _fatalErrors, 0);
        }
        catch (Exception ex)
        {
            // RetryPipelineBehavior already owns retry/dead-letter handling and never rethrows in the normal case —
            // reaching here means that mechanism itself failed. Abandon rather than complete: the delivery count
            // bounds the redelivery, and the message ends up in the dead-letter queue with a reason.
            _logger?.LogCritical(ex, "Unhandled exception escaped the consumer pipeline for message {MessageId}",
                args.Message.MessageId);
            try
            {
                await args.AbandonMessageAsync(args.Message, cancellationToken: CancellationToken.None);
            }
            catch (Exception abandonEx)
            {
                _logger?.LogWarning(abandonEx, "Failed to abandon message {MessageId}", args.Message.MessageId);
            }
        }
        finally
        {
            DeliveryScope.Current = null;
        }
    }

    // Every message on this instance's session is a response to one of its own outstanding requests.
    private async Task HandleReplyAsync(ProcessSessionMessageEventArgs args)
    {
        try
        {
            if (ResolveConnector(args.Message) is { } connector)
                await connector.ProcessResponseMessageAsync(args.Message.Body.ToString(), args.CancellationToken);
        }
        catch (Exception ex)
        {
            // Whoever was waiting on this response has its own timeout (Requester<TRequest>'s CancelAfter), so there's
            // nothing useful to retry — make it visible and move on.
            _logger?.LogCritical(ex, "Unhandled exception processing a response message");
        }
        finally
        {
            await args.CompleteMessageAsync(args.Message, CancellationToken.None);
        }
    }

    public string GetTopicName(Type messageType) =>
        _topicNamesByMessageType.GetOrAdd(messageType, topologyBuilder.ResolveTopicName);

    public async Task PublishAsync(string topic, string messageBody, string messageTypeName,
        OutgoingOptions options = default, CancellationToken token = default)
    {
        var message = new ServiceBusMessage(messageBody)
        {
            ContentType = "application/json",
            ApplicationProperties =
            {
                // Read back on receive to resolve which IClientConnector<TMessage> to dispatch into.
                [ServiceBusConstants.MessageTypeProperty] = messageTypeName
            }
        };
        if (options.MessageId is { } id) message.MessageId = id.ToString();
        if (options.IsRequest)
        {
            message.ReplyTo = ReplyQueueName;
            message.ReplyToSessionId = ReplySessionId;
        }

        if (options.ScheduledEnqueueTime is { } at) message.ScheduledEnqueueTime = at;
        if (options.TargetSubscription is { Length: > 0 } target)
            message.ApplicationProperties[ServiceBusConstants.TargetProperty] = target;

        await GetSender(topic).SendMessageAsync(message, token);
    }

    public async Task PublishReplyAsync(string replyQueue, string sessionId, string messageBody,
        string messageTypeName, CancellationToken token = default)
    {
        var message = new ServiceBusMessage(messageBody)
        {
            ContentType = "application/json", SessionId = sessionId,
            ApplicationProperties =
            {
                [ServiceBusConstants.MessageTypeProperty] = messageTypeName
            }
        };
        await GetSender(replyQueue).SendMessageAsync(message, token);
    }

    private ServiceBusSender GetSender(string entity) =>
        _senders.GetOrAdd(entity, connection.Client.CreateSender);

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        // Nothing to tear down: processors stop with the token StartAsync was given, the connection is owned (and
        // disposed) by IServiceBusConnectionProvider, and the topics and subscriptions are meant to outlive this
        // instance — other instances of the same consumer type keep reading through them.
        return Task.CompletedTask;
    }
}
