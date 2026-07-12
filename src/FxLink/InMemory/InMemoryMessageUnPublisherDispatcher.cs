using System.Collections.Concurrent;

namespace FxLink.InMemory;

internal class InMemoryMessageUnPublisherDispatcher
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _lookup = [];

    internal CancellationToken AcquiredToken(Guid tokenId, TimeSpan delay)
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(delay.Add(TimeSpan.FromMilliseconds(10)));
        _lookup.TryAdd(tokenId, cts);
        return cts.Token;
    }

    internal void CancelToken(Guid tokenId)
    {
        if (!_lookup.TryGetValue(tokenId, out var cts)) return;
        cts.Cancel();
        cts.Dispose();
    }
}