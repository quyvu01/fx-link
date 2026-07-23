using System.Threading.Channels;
using FxLink.Abstractions;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Constants;
using FxLink.RabbitMq.Delegates;
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

    private readonly IMessageKeys _messageKeys = serviceProvider.GetRequiredService<IMessageKeys>();
    private readonly ILogger<RabbitMqClient> _logger = serviceProvider.GetRequiredService<ILogger<RabbitMqClient>>();
    
    private TaskCompletionSource _tcs = new();

    private IConnection _connection;

    private Channel<IChannel> _channelPool;

    private MessageReceivedAsync _messageReceivedAsync;
    private MessageRequestReceivedAsync _messageRequesterConsumerAsync;

    public async Task PublishMessageAsync(MessagePublisher message, CancellationToken token = default)
    {
        var channel = await _channelPool.Reader.ReadAsync(token);
        try
        {
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

    public void MessageConsumed(MessageReceivedAsync messageReceivedAsync) =>
        _messageReceivedAsync += messageReceivedAsync;

    public void MessageRequesterConsumer(MessageRequestReceivedAsync messageReceivedAsync) =>
        _messageRequesterConsumerAsync += messageReceivedAsync;

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
        await ChannelsInitializeAsync();

        var replyChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var queueDeclareResult = await replyChannel.QueueDeclareAsync(cancellationToken: cancellationToken);
        ReplyQueueName = queueDeclareResult.QueueName;
        var requestConsumer = new AsyncEventingBasicConsumer(replyChannel);
        requestConsumer.ReceivedAsync += async (sender, ea) =>
        {
            if (_messageRequesterConsumerAsync is not { } handlerAsync) return;
            var tasks = handlerAsync.GetInvocationList()
                .Cast<MessageRequestReceivedAsync>()
                .Select(h => h.Invoke(sender, ea, cancellationToken));
            await Task.WhenAll(tasks);
        };
        await replyChannel.BasicConsumeAsync(ReplyQueueName, false, requestConsumer, cancellationToken);

        var messageKeys = _messageKeys.GetMessageKeys();

        // Force IClientConnector<TMessage> to be constructed for every message type this process
        // consumes, so its MessageConsumed handler gets wired up even if this process never
        // publishes/requests that type itself (publish-side resolution is otherwise lazy).
        foreach (var messageType in messageKeys.Keys)
        {
            var connectorType = typeof(IClientConnector<>).MakeGenericType(messageType);
            serviceProvider.GetRequiredService(connectorType);
        }

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

                    var deadLetterExchange = g.MessageType.GetDeadLetterExchangeName();

                    // Declare dead letter exchanges and queues
                    await declareChannel.ExchangeDeclareAsync(deadLetterExchange, ExchangeType.Fanout,
                        durable: false, cancellationToken: cancellationToken);

                    await declareChannel.QueueBindAsync(deadLetterQueue, deadLetterExchange, string.Empty,
                        cancellationToken: cancellationToken);

                    // Delay topology only applies when a delay provider is configured (via e.g.
                    // UseRabbitMqDelayScheduler()) AND that provider needs RabbitMQ topology at
                    // all (a Quartz/Hangfire/Redis-backed provider wouldn't implement this).
                    var delayMessageProvider = serviceProvider.GetService<IDelayMessageProvider>();
                    if (delayMessageProvider is IRabbitMqDelayTopology delayTopology)
                        await delayTopology.DeclareTopologyAsync(declareChannel, g.MessageType, queueName,
                            cancellationToken);
                }

                var receivedEndpointChannel = await _connection
                    .CreateChannelAsync(cancellationToken: cancellationToken);

                foreach (var _ in c)
                {
                    var consumer = new AsyncEventingBasicConsumer(receivedEndpointChannel);
                    consumer.ReceivedAsync += async (sender, ea) =>
                    {
                        if (_messageReceivedAsync is not { } handlerAsync) return;
                        var tasks = handlerAsync.GetInvocationList().Cast<MessageReceivedAsync>()
                            .Select(h => h.Invoke(sender, ea, consumerType, cancellationToken));
                        await Task.WhenAll(tasks);
                    };

                    await receivedEndpointChannel.BasicConsumeAsync(queueName, false, consumer,
                        cancellationToken: cancellationToken);
                }
            });
        foreach (var task in tasks) await task;

        _connection.ConnectionShutdownAsync += (_, @event) =>
        {
            _tcs.TrySetException(@event.Exception ?? new Exception("Connection or Channel is shutdown!"));
            return Task.CompletedTask;
        };
        try
        {
            await _tcs.Task;
        }
        catch (Exception)
        {
            _tcs = new TaskCompletionSource();
            throw;
        }
    }


    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
    }

    private async Task ChannelsInitializeAsync()
    {
        var poolSize = Math.Min(Environment.ProcessorCount, 8); // Todo, update poolSize later
        var options = new BoundedChannelOptions(poolSize) { FullMode = BoundedChannelFullMode.Wait };
        _channelPool = Channel.CreateBounded<IChannel>(options);
        for (var i = 0; i < poolSize; i++)
        {
            var channel = await CreatePublishChannelAsync();
            await _channelPool.Writer.WriteAsync(channel);
        }
    }
}