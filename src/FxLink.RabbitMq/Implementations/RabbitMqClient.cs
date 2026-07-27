using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;
using FxLink.Abstractions;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Constants;
using FxLink.RabbitMq.Entities;
using FxLink.RabbitMq.Extensions;
using FxLink.RabbitMq.Registries;
using FxLink.Registries;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FxLink.RabbitMq.Implementations;

internal class RabbitMqClient(IServiceProvider serviceProvider) : IRabbitMqClient, IMessageBrokerConnector
{
    private readonly IRabbitMqConfiguration _rabbitMqConfiguration =
        serviceProvider.GetRequiredService<IRabbitMqConfiguration>();

    private static readonly ConcurrentDictionary<Type, MethodInfo> GetConsumerDispatchInternalMethodCache = [];

    private readonly ConcurrentDictionary<string, Lazy<Task>> _declaredExchanges = [];
    private readonly ConcurrentDictionary<string, Type> _connectorTypeCache = [];
    private readonly IMessageKeys _messageKeys = serviceProvider.GetRequiredService<IMessageKeys>();
    private readonly ILogger<RabbitMqClient> _logger = serviceProvider.GetRequiredService<ILogger<RabbitMqClient>>();

    private IConnection _connection;

    private Channel<IChannel> _channelPool;

    public async Task PublishMessageAsync(MessagePublisher message, CancellationToken token = default)
    {
        var channel = await _channelPool.Reader.ReadAsync(token);
        try
        {
            if (message.ExchangeName is { Length: > 0 } exchangeName) await EnsureExchangeDeclaredAsync(exchangeName);
            await channel.BasicPublishAsync(message.ExchangeName, message.RoutingKey, mandatory: message.Mandatory,
                basicProperties: message.Props, message.MessageBody, token);
        }
        finally
        {
            if (channel.IsOpen)
            {
                await _channelPool.Writer.WriteAsync(channel, token);
            }
            else
            {
                _logger.LogWarning(
                    "Publish channel was closed by the broker while publishing to exchange {ExchangeName}; replacing it in the pool",
                    message.ExchangeName);
                await _channelPool.Writer.WriteAsync(await CreatePublishChannelAsync(token), token);
            }
        }
    }

    public async Task DeclareExchangeAsync(string exchangeName, CancellationToken token = default)
    {
        // Short-lived scratch channel, same reasoning as the consumer-side declares in StartAsync:
        // a declare failure here must not risk taking down a pooled publish channel.
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: token);
        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: false,
            cancellationToken: token);
    }

    private async Task<IChannel> CreatePublishChannelAsync(CancellationToken cancellationToken = default)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        channel.BasicReturnAsync += (_, ea) =>
        {
            _logger.LogWarning(
                "Message returned as unroutable. Exchange={Exchange} RoutingKey={RoutingKey} ReplyCode={ReplyCode} ReplyText={ReplyText}",
                ea.Exchange, ea.RoutingKey, ea.ReplyCode, ea.ReplyText);
            return Task.CompletedTask;
        };
        return channel;
    }

    public string ReplyQueueName { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var userName = _rabbitMqConfiguration.RabbitMqUserName ?? RabbitMqConstants.DefaultUserName;
        var password = _rabbitMqConfiguration.RabbitMqPassword ?? RabbitMqConstants.DefaultPassword;
        var connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitMqConfiguration.RabbitMqHost,
            VirtualHost = _rabbitMqConfiguration.RabbitMqVirtualHost,
            Port = _rabbitMqConfiguration.RabbitMqPort,
            Ssl = _rabbitMqConfiguration.SslOption ?? new SslOption(),
            UserName = userName,
            Password = password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        // AutomaticRecoveryEnabled reconnects transparently and retries indefinitely on its own —
        // an ordinary connection drop is not a failure the supervisor needs to know about. These
        // handlers are purely observational; StartAsync must keep running through them (see the
        // Task.Delay wait at the bottom of this method).
        _connection.ConnectionShutdownAsync += (_, args) =>
        {
            _logger.LogWarning(
                "RabbitMQ connection shut down (ReplyText={ReplyText}); AutomaticRecoveryEnabled will attempt to reconnect.",
                args.ReplyText);
            return Task.CompletedTask;
        };
        _connection.RecoverySucceededAsync += (_, _) =>
        {
            _logger.LogInformation("RabbitMQ connection recovered successfully.");
            return Task.CompletedTask;
        };
        _connection.ConnectionRecoveryErrorAsync += (_, args) =>
        {
            _logger.LogWarning(args.Exception, "RabbitMQ connection recovery attempt failed; will retry.");
            return Task.CompletedTask;
        };

        // ReplyQueueName is server-named (declared with an empty name). On automatic connection
        // recovery, RabbitMQ.Client redeclares it and the broker assigns a brand-new name — the
        // old one is gone. Without this handler, ReplyQueueName would stay stuck on the old,
        // now-nonexistent name forever, and every response publish would NO_ROUTE from that point
        // on, since no queue by that name exists anymore.
        _connection.QueueNameChangedAfterRecoveryAsync += (_, args) =>
        {
            if (args.NameBefore != ReplyQueueName) return Task.CompletedTask;
            _logger.LogInformation(
                "Reply queue name changed after connection recovery: {OldName} -> {NewName}",
                args.NameBefore, args.NameAfter);
            ReplyQueueName = args.NameAfter;
            return Task.CompletedTask;
        };

        await ChannelsInitializeAsync();

        // Recyclable: AutomaticRecoveryEnabled only recovers when the *connection* drops. A channel
        // can also be closed on its own (broker-side protocol error, ...) while the connection stays
        // up, and nothing would ever reopen it. Recycle<T> makes that self-healing instead — every
        // time the reply channel is needed, GetChannelAsync() transparently hands back a live one,
        // re-running the queue-declare + consumer setup if the previous one died.
        var replyChannelRecycle = new Recycle<RecyclableChannel>(() => new RecyclableChannel(_connection,
            async channel =>
            {
                var replyQueue = await channel.QueueDeclareAsync(durable: false, exclusive: true,
                    autoDelete: false, arguments: null, cancellationToken: cancellationToken);
                ReplyQueueName = replyQueue.QueueName;
                var replyConsumer = new AsyncEventingBasicConsumer(channel);
                replyConsumer.ReceivedAsync += async (sender, ea) =>
                {
                    if (ea.BasicProperties.Type is not { Length: > 0 } messageType) return;
                    var connectorType = _connectorTypeCache.GetOrAdd(messageType, static type =>
                    {
                        var msgType = Type.GetType(type);
                        return msgType is null ? null : typeof(IClientConnector<>).MakeGenericType(msgType);
                    });
                    if (connectorType is null) return;
                    var connector = (AbstractRabbitMqConnector)serviceProvider
                        .GetRequiredService(connectorType);
                    var ackChannel = ((AsyncEventingBasicConsumer)sender).Channel;
                    try
                    {
                        await connector.ProcessResponseMessageAsync(ea);
                    }
                    catch (Exception ex)
                    {
                        // RetryPipelineBehavior already owns retry/dead-letter handling and never
                        // rethrows in the normal case — reaching here means that mechanism itself
                        // failed (e.g. IPublisher missing, publish channel down). Ack anyway to
                        // avoid an uncontrolled redelivery loop; the failure is only visible via
                        // this log.
                        _logger.LogCritical(ex,
                            "Unhandled exception escaped the consumer pipeline for {MessageType}",
                            messageType);
                    }

                    await ackChannel.BasicAckAsync(ea.DeliveryTag, false, ea.CancellationToken);
                };
                await channel.BasicConsumeAsync(ReplyQueueName, false, replyConsumer,
                    cancellationToken: cancellationToken);
            }, null, cancellationToken));

        await replyChannelRecycle.Current.GetChannelAsync();
        _ = MonitorRecycleAsync(replyChannelRecycle, cancellationToken);

        var messageKeys = _messageKeys.GetMessageKeys();

        var tasks = messageKeys
            .Select(x => x.Value.Select(k => new
            {
                ConsumerType = k, MessageType = x.Key
            }))
            .SelectMany(a => a)
            .GroupBy(a => a.ConsumerType)
            .Select(async c =>
            {
                var consumerType = c.Key;
                var messageTypes = c.Select(g => g.MessageType).ToArray();
                var queueName = consumerType.GetConsumerName();
                var deadLetterQueue = consumerType.GetDeadLetterConsumerName();

                await DeclareTopologyAsync();

                var dispatchDefinition = GetConsumerDispatchDefinition(consumerType);
                var prefetchCount = dispatchDefinition.PrefetchCount;
                var concurrentMessageLimit = dispatchDefinition.ConcurrentMessageLimit;
                var createChannelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: concurrentMessageLimit);

                // Recyclable, same reasoning as the reply channel above: AutomaticRecoveryEnabled only
                // covers connection drops, not this specific channel getting closed on its own. Every
                // time it's (re)created, re-verify topology and resubscribe from scratch.
                var consumerChannelRecycle = new Recycle<RecyclableChannel>(() => new RecyclableChannel(_connection,
                    async channel =>
                    {
                        await DeclareTopologyAsync();

                        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: prefetchCount,
                            global: false, cancellationToken);

                        var consumer = new AsyncEventingBasicConsumer(channel);
                        consumer.ReceivedAsync += async (sender, ea) =>
                        {
                            if (ea.BasicProperties.Type is not { Length: > 0 } messageType) return;
                            var connectorType = _connectorTypeCache.GetOrAdd(messageType, static type =>
                            {
                                var msgType = Type.GetType(type);
                                return msgType is null ? null : typeof(IClientConnector<>).MakeGenericType(msgType);
                            });
                            if (connectorType is null) return;
                            var connector = (AbstractRabbitMqConnector)serviceProvider
                                .GetRequiredService(connectorType);
                            var ackChannel = ((AsyncEventingBasicConsumer)sender).Channel;
                            try
                            {
                                await connector.ProcessMessageReceivedAsync(ea, consumerType);
                            }
                            catch (Exception ex)
                            {
                                // RetryPipelineBehavior already owns retry/dead-letter handling and never
                                // rethrows in the normal case — reaching here means that mechanism itself
                                // failed (e.g. IPublisher missing, publish channel down). Ack anyway to
                                // avoid an uncontrolled redelivery loop; the failure is only visible via
                                // this log.
                                _logger.LogCritical(ex,
                                    "Unhandled exception escaped the consumer pipeline for {MessageType}",
                                    messageType);
                            }

                            await ackChannel.BasicAckAsync(ea.DeliveryTag, false, ea.CancellationToken);
                        };

                        await channel.BasicConsumeAsync(queueName, false, consumer,
                            cancellationToken: cancellationToken);
                    }, createChannelOptions, cancellationToken));

                await consumerChannelRecycle.Current.GetChannelAsync();
                _ = MonitorRecycleAsync(consumerChannelRecycle, cancellationToken);
                return;

                // Topology declare uses its own short-lived scratch channel (same reasoning as
                // DeclareExchangeAsync): a declare failure must not risk taking down the long-lived
                // consuming channel below. Idempotent, so it's safe to rerun every time the
                // consumer channel is (re)created — see the setup callback further down.
                async Task DeclareTopologyAsync()
                {
                    await using var declareChannel = await _connection
                        .CreateChannelAsync(cancellationToken: cancellationToken);
                    await declareChannel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false,
                        autoDelete: false, arguments: null, cancellationToken: cancellationToken);

                    await declareChannel.QueueDeclareAsync(deadLetterQueue, durable: false, exclusive: false,
                        autoDelete: false, cancellationToken: cancellationToken);

                    foreach (var messageType in messageTypes)
                    {
                        var exchangeName = messageType.GetExchangeName();
                        // Declare main exchanges and queues
                        await declareChannel.ExchangeDeclareAsync(exchangeName, type: ExchangeType.Fanout,
                            durable: false, cancellationToken: cancellationToken);
                        await declareChannel.QueueBindAsync(queue: queueName, exchangeName, string.Empty,
                            cancellationToken: cancellationToken);

                        var retryExchange = messageType.GetRetryExchangeName();

                        // Declare retry exchanges and queues
                        await declareChannel.ExchangeDeclareAsync(retryExchange, ExchangeType.Fanout,
                            durable: false, cancellationToken: cancellationToken);

                        var retryQueue = consumerType.GetRetryConsumerName(messageType);
                        await declareChannel.QueueDeclareAsync(retryQueue, durable: false, exclusive: false,
                            autoDelete: false, arguments: new Dictionary<string, object>
                            {
                                ["x-dead-letter-exchange"] = exchangeName,
                                ["x-dead-letter-routing-key"] = string.Empty
                            }, cancellationToken: cancellationToken);
                        await declareChannel.QueueBindAsync(retryQueue, retryExchange, string.Empty,
                            cancellationToken: cancellationToken);

                        var deadLetterExchange = messageType.GetDeadLetterExchangeName();

                        // Declare dead letter exchanges and queues
                        await declareChannel.ExchangeDeclareAsync(deadLetterExchange, ExchangeType.Fanout,
                            durable: false, cancellationToken: cancellationToken);

                        await declareChannel.QueueBindAsync(deadLetterQueue, deadLetterExchange, string.Empty,
                            cancellationToken: cancellationToken);

                        var delayMessageProvider = serviceProvider.GetService<IDelayMessageProvider>();
                        if (delayMessageProvider is IRabbitMqDelayTopology delayTopology)
                            await delayTopology.DeclareTopologyAsync(declareChannel, messageType, queueName,
                                cancellationToken);
                    }
                }
            });
        await Task.WhenAll(tasks);

        // Run until StopAsync/host shutdown cancels this token. Deliberately not tied to
        // ConnectionShutdownAsync — see the handler registered above for why.
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    // Reply channel and consumer channels are consume-only: nothing calls GetChannelAsync() again on
    // its own once BasicConsumeAsync is set up, so nobody would ever notice (or act on) Stopping
    // firing. This loop is what turns any Recycle<RecyclableChannel> into an actual self-healing
    // consumer: wait for the current one to die, eagerly pull the replacement (which lazily
    // recreates + reruns topology-declare/consume setup), then go wait on whatever is current now.
    private static async Task MonitorRecycleAsync(Recycle<RecyclableChannel> recycle,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var dying = recycle.Current;
            await dying.Stopping;
            if (cancellationToken.IsCancellationRequested) return;

            // Recycle<T> swaps Current to a fresh, not-yet-created instance from its own
            // continuation on this same Stopping task — spin until that's visible here too
            // (single reference write racing another continuation, resolves in a tick or two).
            RecyclableChannel next;
            while (ReferenceEquals(next = recycle.Current, dying)) await Task.Yield();

            await next.GetChannelAsync();
        }
    }


    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
    }

    private async Task ChannelsInitializeAsync()
    {
        var poolSize = _rabbitMqConfiguration.PublishChannelPoolSize;
        var options = new BoundedChannelOptions(poolSize) { FullMode = BoundedChannelFullMode.Wait };
        _channelPool = Channel.CreateBounded<IChannel>(options);
        for (var i = 0; i < poolSize; i++)
        {
            var channel = await CreatePublishChannelAsync();
            await _channelPool.Writer.WriteAsync(channel);
        }
    }

    private Task EnsureExchangeDeclaredAsync(string exchangeName)
    {
        var lazy = _declaredExchanges.GetOrAdd(exchangeName,
            name => new Lazy<Task>(() => DeclareExchangeAsync(name)));
        // Don't let a transient failure permanently poison this exchange for the process
        // lifetime — drop the cached attempt so the next publish retries the declaration.
        if (lazy.Value.IsFaulted) _declaredExchanges.TryRemove(exchangeName, out _);
        return lazy.Value;
    }

    private IConsumerDispatchDefinition GetConsumerDispatchDefinition(Type consumerType)
    {
        var method = GetConsumerDispatchInternalMethodCache.GetOrAdd(consumerType, BuildGetConsumerDispatchInternalMethod);
        return (IConsumerDispatchDefinition)method.Invoke(this, null);
    }

    private static MethodInfo BuildGetConsumerDispatchInternalMethod(Type consumerType)
    {
        var openMethod = typeof(RabbitMqClient).GetMethod(
            nameof(GetConsumerDispatchInternal), BindingFlags.NonPublic | BindingFlags.Instance)!;
        return openMethod.MakeGenericMethod(consumerType);
    }

    private IConsumerDispatchDefinition GetConsumerDispatchInternal<TConsumer>() where TConsumer : IConsumer
    {
        try
        {
            var configurator = serviceProvider.GetService<IConsumerConfigurator<TConsumer>>();
            if (configurator is null) return ConsumerDispatchDefinition.FromConfiguration(_rabbitMqConfiguration);
            var consumerConfigurator = (ConsumerConfigurator<TConsumer>)configurator!;
            var consumerDispatchConfig = consumerConfigurator
                .GetConfigurator<IConsumerDispatchDefinition>(typeof(TConsumer));
            if (consumerDispatchConfig is not null) return consumerDispatchConfig;
            return ConsumerDispatchDefinition.FromConfiguration(_rabbitMqConfiguration);
        }
        catch (Exception e)
        {
            _logger.LogWarning("Failed to load consumer dispatch settings: {@Exception}, return default instead!",
                new { e.Message, e.StackTrace });
            return ConsumerDispatchDefinition.FromConfiguration(_rabbitMqConfiguration);
        }
    }
}