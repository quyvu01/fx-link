using RabbitMQ.Client.Events;

namespace FxLink.RabbitMq.Delegates;

internal delegate Task MessageReceivedAsync(object sender, BasicDeliverEventArgs ea,
    CancellationToken token = default);