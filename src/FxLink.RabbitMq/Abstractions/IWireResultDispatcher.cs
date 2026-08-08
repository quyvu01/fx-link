namespace FxLink.RabbitMq.Abstractions;

internal interface IWireResultDispatcher<TResponse> where TResponse : class
{
    void SetResult(string json, CancellationToken token = default);
}