using System.Collections.Concurrent;
using System.Threading.Channels;
using FxLink.Abstractions;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Constants;
using FxLink.RabbitMq.Entities;
using FxLink.RabbitMq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FxLink.RabbitMq.Implementations;

internal class RabbitMqClient(IServiceProvider serviceProvider) : IRabbitMqClient, IMessageBrokerConnector
{
    private readonly IRabbitMqConfiguration _rabbitMqConfiguration =
        serviceProvider.GetRequiredService<IRabbitMqConfiguration>();

    private readonly ConcurrentDictionary<string, Lazy<Task>> _declaredExchanges = new();

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
            VirtualHost = _rabbitMqConfiguration.RabbitVirtualHost,
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

        var replyChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var replyQueue = await replyChannel.QueueDeclareAsync(durable: false, exclusive: true,
            autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        ReplyQueueName = replyQueue.QueueName;
        var replyConsumer = new AsyncEventingBasicConsumer(replyChannel);
        replyConsumer.ReceivedAsync += async (sender, ea) =>
        {
            if (ea.BasicProperties.Type is not { Length: > 0 } messageType) return;
            var msgType = Type.GetType(messageType);
            if (msgType is null) return;
            var connector = (AbstractRabbitMqConnector)serviceProvider.GetRequiredService(typeof(IClientConnector<>)
                .MakeGenericType(msgType));
            await connector.ProcessResponseMessageAsync(ea);
            var channel = ((AsyncEventingBasicConsumer)sender).Channel;
            await channel.BasicAckAsync(ea.DeliveryTag, false, ea.CancellationToken);
        };
        await replyChannel.BasicConsumeAsync(ReplyQueueName, false, replyConsumer, cancellationToken);

        var messageKeys = _messageKeys.GetMessageKeys();

        // Force IClientConnector<TMessage> to be constructed for every message type this process
        // consumes, so its MessageConsumed handler gets wired up even if this process never
        // publishes/requests that type itself (publish-side resolution is otherwise lazy).
        foreach (var messageType in messageKeys.Keys)
            serviceProvider.GetRequiredService(typeof(IClientConnector<>).MakeGenericType(messageType));

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
                var queueName = consumerType.GetConsumerName();
                var deadLetterQueue = consumerType.GetDeadLetterConsumerName();

                await using var declareChannel = await _connection
                    .CreateChannelAsync(cancellationToken: cancellationToken);
                await declareChannel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false,
                    autoDelete: false, arguments: null, cancellationToken: cancellationToken);

                await declareChannel.QueueDeclareAsync(deadLetterQueue, durable: false, exclusive: false,
                    autoDelete: false, cancellationToken: cancellationToken);

                foreach (var g in c)
                {
                    var exchangeName = g.MessageType.GetExchangeName();
                    // Declare main exchanges and queues
                    await declareChannel.ExchangeDeclareAsync(exchangeName, type: ExchangeType.Fanout,
                        durable: false, cancellationToken: cancellationToken);
                    await declareChannel.QueueBindAsync(queue: queueName, exchangeName, string.Empty,
                        cancellationToken: cancellationToken);

                    var retryExchange = g.MessageType.GetRetryExchangeName();

                    // Declare retry exchanges and queues
                    await declareChannel.ExchangeDeclareAsync(retryExchange, ExchangeType.Fanout,
                        durable: false, cancellationToken: cancellationToken);

                    var retryQueue = consumerType.GetRetryConsumerName(g.MessageType);
                    await declareChannel.QueueDeclareAsync(retryQueue, durable: false, exclusive: false,
                        autoDelete: false, arguments: new Dictionary<string, object>
                        {
                            ["x-dead-letter-exchange"] = exchangeName,
                            ["x-dead-letter-routing-key"] = string.Empty
                        }, cancellationToken: cancellationToken);
                    await declareChannel.QueueBindAsync(retryQueue, retryExchange, string.Empty,
                        cancellationToken: cancellationToken);

                    var deadLetterExchange = g.MessageType.GetDeadLetterExchangeName();

                    // Declare dead letter exchanges and queues
                    await declareChannel.ExchangeDeclareAsync(deadLetterExchange, ExchangeType.Fanout,
                        durable: false, cancellationToken: cancellationToken);

                    await declareChannel.QueueBindAsync(deadLetterQueue, deadLetterExchange, string.Empty,
                        cancellationToken: cancellationToken);

                    var delayMessageProvider = serviceProvider.GetService<IDelayMessageProvider>();
                    if (delayMessageProvider is IRabbitMqDelayTopology delayTopology)
                        await delayTopology.DeclareTopologyAsync(declareChannel, g.MessageType, queueName,
                            cancellationToken);
                }

                var receivedEndpointChannel = await _connection
                    .CreateChannelAsync(cancellationToken: cancellationToken);
                var consumer = new AsyncEventingBasicConsumer(receivedEndpointChannel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    if (ea.BasicProperties.Type is not { Length: > 0 } messageType) return;
                    var msgType = Type.GetType(messageType);
                    if (msgType is null) return;
                    var connector = (AbstractRabbitMqConnector)serviceProvider
                        .GetRequiredService(typeof(IClientConnector<>).MakeGenericType(msgType));
                    await connector.ProcessMessageReceivedAsync(ea, consumerType);
                    var channel = ((AsyncEventingBasicConsumer)sender).Channel;
                    await channel.BasicAckAsync(ea.DeliveryTag, false, ea.CancellationToken);
                };

                await receivedEndpointChannel.BasicConsumeAsync(queueName, false, consumer,
                    cancellationToken: cancellationToken);
            });
        await Task.WhenAll(tasks);

        // Run until StopAsync/host shutdown cancels this token. Deliberately not tied to
        // ConnectionShutdownAsync — see the handler registered above for why.
        await Task.Delay(Timeout.Infinite, cancellationToken);
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
}