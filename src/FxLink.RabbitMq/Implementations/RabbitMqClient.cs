using FxLink.Abstractions;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Constants;
using FxLink.RabbitMq.Delegates;
using FxLink.RabbitMq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FxLink.RabbitMq.Implementations;

internal class RabbitMqClient(IServiceProvider serviceProvider) : IRabbitMqClient, IMessageBrokerConnector
{
    private readonly IRabbitMqConfiguration _rabbitMqConfiguration =
        serviceProvider.GetRequiredService<IRabbitMqConfiguration>();

    private readonly IMessageKeys _messageKeys = serviceProvider.GetRequiredService<IMessageKeys>();

    private readonly SemaphoreSlim _semaphore = new(128, 128);
    public IConnection Connection { get; private set; }
    public IChannel Channel { get; private set; }

    private MessageReceivedAsync _messageReceivedAsync;
    private MessageReceivedAsync _messageRequesterConsumerAsync;

    public void MessageConsumed(MessageReceivedAsync messageReceivedAsync) =>
        _messageReceivedAsync += messageReceivedAsync;

    public void MessageRequesterConsumer(MessageReceivedAsync messageReceivedAsync) =>
        _messageRequesterConsumerAsync = messageReceivedAsync;

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

        Connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        Channel = await Connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var queueDeclareResult = await Channel.QueueDeclareAsync(cancellationToken: cancellationToken);
        ReplyQueueName = queueDeclareResult.QueueName;
        var requestConsumer = new AsyncEventingBasicConsumer(Channel);
        requestConsumer.ReceivedAsync += async (sender, ea) =>
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_messageRequesterConsumerAsync is { } handlerAsync)
                    await handlerAsync.Invoke(sender, ea, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        };
        await Channel.BasicConsumeAsync(ReplyQueueName, true, requestConsumer, cancellationToken);
        
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
                var queueName = (c.Key as Type).GetConsumerName();
                await Channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false,
                    autoDelete: false, arguments: null, cancellationToken: cancellationToken);

                var exchangeNames = c.Select(opt => opt.MessageType.GetExchangeName());

                foreach (var exchangeName in exchangeNames)
                {
                    await Channel.ExchangeDeclareAsync(exchangeName, type: ExchangeType.Fanout,
                        cancellationToken: cancellationToken);
                    await Channel.QueueBindAsync(queue: queueName, exchangeName, "",
                        cancellationToken: cancellationToken);
                }

                var consumer = new AsyncEventingBasicConsumer(Channel);

                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    await _semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        if (_messageReceivedAsync is { } handlerAsync)
                            await handlerAsync.Invoke(sender, ea, cancellationToken);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                };

                await Channel.BasicConsumeAsync(queueName, false, consumer, cancellationToken: cancellationToken);
            });

        await Task.WhenAll(tasks);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Channel is not null) await Channel.CloseAsync(cancellationToken);
        if (Connection is not null) await Connection.CloseAsync(cancellationToken);
        _semaphore?.Dispose();
    }
}