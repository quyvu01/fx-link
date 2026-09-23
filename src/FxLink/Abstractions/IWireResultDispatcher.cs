namespace FxLink.Abstractions;

internal interface IWireResultDispatcher
{
    void SetResult(string json, CancellationToken token = default);
}
internal interface IWireResultDispatcher<TResponse> : IWireResultDispatcher where TResponse : class;
