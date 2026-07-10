using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Extensions;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.Constants;
using FxLink.RabbitMq.Extensions;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FxLink.RabbitMq.Implementations;

internal class RabbitmqConnector(IServiceProvider serviceProvider) :
    IMessageBrokerConnector,
    IRequestMessage,
    IPublishMessage,
    IConsumeMessage
{
    private readonly ILogger<RabbitmqConnector> _logger = serviceProvider.GetService<ILogger<RabbitmqConnector>>();

    private readonly IRabbitMqConfiguration _rabbitMqConfiguration =
        serviceProvider.GetRequiredService<IRabbitMqConfiguration>();

    private readonly IMessageKeys _messageKeys = serviceProvider.GetRequiredService<IMessageKeys>();

    private readonly Channel<MessageData> _messageDataChannel = Channel.CreateUnbounded<MessageData>();

    private readonly SemaphoreSlim _semaphore = new(128, 128);

    private IConnection _connection;
    private IChannel _channel;

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
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

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
                var queueName = (c.Key as Type).GetConsumerName();
                await _channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false,
                    autoDelete: false, arguments: null, cancellationToken: cancellationToken);

                var exchangeNames = c.Select(opt => opt.MessageType.GetExchangeName());

                foreach (var exchangeName in exchangeNames)
                {
                    await _channel.ExchangeDeclareAsync(exchangeName, type: ExchangeType.Fanout,
                        cancellationToken: cancellationToken);
                    await _channel.QueueBindAsync(queue: queueName, exchangeName, "",
                        cancellationToken: cancellationToken);
                }

                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    await _semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        await ProcessMessageAsync(sender, ea, cancellationToken);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                };

                await _channel.BasicConsumeAsync(queueName, false, consumer, cancellationToken: cancellationToken);
            });

        await Task.WhenAll(tasks);
    }

    private async Task ProcessMessageAsync(object sender, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var cons = (AsyncEventingBasicConsumer)sender;
        var ch = cons.Channel;
        var body = ea.Body.ToArray();
        var props = ea.BasicProperties;
        var replyProps = new BasicProperties { CorrelationId = props.CorrelationId };
        // Create timeout CTS
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var cancellationToken = cts.Token;
        try
        {
            await _messageDataChannel.Writer.WriteAsync(new MessageData(body, props.Type,
                    Guid.Parse(props.CorrelationId!), props.Headers?.ToDictionary() ?? [], cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger?.LogWarning("Request timeout for <{DistributedKey}>", props.Type);
            if (props.ReplyTo is null) return;
            var response = Result.Failed(new TimeoutException($"Request timeout for {props.Type}"));
            await SendResponseAsync(ch, props.ReplyTo, replyProps, response, cancellationToken);
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Error while responding <{DistributedKey}>", props.Type);
            if (props.ReplyTo is null) return;
            var response = Result.Failed(e);
            await SendResponseAsync(ch, props.ReplyTo, replyProps, response, stoppingToken);
        }
        finally
        {
            try
            {
                await ch.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to acknowledge message");
            }
        }
    }

    private static async Task SendResponseAsync(IChannel ch, string replyTo, BasicProperties replyProps,
        Result response, CancellationToken cancellationToken)
    {
        try
        {
            var responseAsString = JsonSerializer.Serialize(response);
            var responseBytes = Encoding.UTF8.GetBytes(responseAsString);
            await ch.BasicPublishAsync(exchange: string.Empty, routingKey: replyTo!,
                mandatory: true, basicProperties: replyProps, body: responseBytes,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Ignore errors when sending error response
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
        _semaphore?.Dispose();
    }

    public Task<Result> RequestMessageAsync<TRequest>(TRequest request, IRequestContext context,
        CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public async Task PublishMessageAsync<TRequest>(TRequest request, IPublisherContext context,
        CancellationToken token = default)
    {
        var exchangeName = typeof(TRequest).GetExchangeName();
        var props = new BasicProperties
        {
            CorrelationId = context.CorrelationId.ToString(),
            Type = typeof(TRequest).AssemblyQualifiedName,
        };
        props.Headers ??= new Dictionary<string, object>();
        context.Headers?.ForEach(h => props.Headers.Add(h.Key, h.Value));
        var messageSerialize = JsonSerializer.Serialize(request);
        var messageBytes = Encoding.UTF8.GetBytes(messageSerialize);
        await _channel.BasicPublishAsync(exchangeName, routingKey: string.Empty,
            mandatory: true, basicProperties: props, body: messageBytes, cancellationToken: token);
    }

    public Channel<MessageData> MessageChannel() => _messageDataChannel;
}