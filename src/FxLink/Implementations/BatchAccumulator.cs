using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Registries;

namespace FxLink.Implementations;

// Buffers messages per group-key until a flush condition is met (MessageLimit reached, TimeLimit
// elapsed, or a retried message forces an early flush), then hands the completed batch to
// flushHandler. Owns nothing about *how* a batch gets dispatched — that's the caller's concern
// (see BatchAccumulatorFactory, which supplies a flushHandler that re-enters the consumer pipeline).
//
// Lifetime: must be a singleton for a given (TMessage, consumerType) — every AddAsync call has to
// see the same buffers, or nothing ever accumulates. See ConsumerPipelineBehaviorOrchestrator for
// why a per-message DI scope would break this.
internal sealed class BatchAccumulator<TMessage> : IBatchAccumulator<TMessage>, IDisposable
    where TMessage : class
{
    private static readonly object UngroupedKey = new();

    private readonly MessageBatchConfigurator _config;
    private readonly Func<IReadOnlyList<IConsumeContext<TMessage>>, CancellationToken, Task> _flushHandler;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<object, BatchState> _states = new();

    public BatchAccumulator(MessageBatchConfigurator config,
        Func<IReadOnlyList<IConsumeContext<TMessage>>, CancellationToken, Task> flushHandler)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(flushHandler);
        _config = config;
        _flushHandler = flushHandler;
        _concurrencyGate = new SemaphoreSlim(config.ConcurrentLimit, config.ConcurrentLimit);
    }

    public Task AddAsync(IConsumeContext<TMessage> context, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var key = ResolveKey(context);
        var state = _states.GetOrAdd(key, _ => new BatchState());
        var forceFlush = context.Headers.Get<int>(DistributedConfigurators.Headers.RetryCountKey) > 0;

        IReadOnlyList<IConsumeContext<TMessage>> snapshot = null;
        lock (state.Lock)
        {
            state.Buffer.Add(context);

            if (state.Buffer.Count == 1)
                state.Timer = new Timer(_ => OnTimerElapsed(state, token), null, _config.TimeLimit,
                    Timeout.InfiniteTimeSpan);
            else if (_config.TimeLimitStart == BatchTimeLimitStart.FromLast)
                state.Timer.Change(_config.TimeLimit, Timeout.InfiniteTimeSpan);

            if (state.Buffer.Count >= _config.MessageLimit || forceFlush)
                snapshot = TakeBufferAndStopTimer(state);
        }

        if (snapshot is { Count: > 0 })
            _ = FlushAsync(snapshot, token);

        return Task.CompletedTask;
    }

    private void OnTimerElapsed(BatchState state, CancellationToken token)
    {
        IReadOnlyList<IConsumeContext<TMessage>> snapshot;
        lock (state.Lock)
        {
            // The timer can race a concurrent AddAsync that already flushed via MessageLimit and
            // started a fresh timer for the next cycle — nothing to do in that case.
            if (state.Buffer.Count == 0) return;
            snapshot = TakeBufferAndStopTimer(state);
        }

        _ = FlushAsync(snapshot, token);
    }

    private static List<IConsumeContext<TMessage>> TakeBufferAndStopTimer(BatchState state)
    {
        var snapshot = state.Buffer;
        state.Buffer = [];
        state.Timer?.Dispose();
        state.Timer = null;
        return snapshot;
    }

    private object ResolveKey(IConsumeContext<TMessage> context)
    {
        if (_config.GroupKeyProvider is not { } provider) return UngroupedKey;
        return provider.TryGetKey(context, out var key) ? key : UngroupedKey;
    }

    private async Task FlushAsync(IReadOnlyList<IConsumeContext<TMessage>> snapshot, CancellationToken token)
    {
        await _concurrencyGate.WaitAsync(token);
        try
        {
            await _flushHandler.Invoke(snapshot, token);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    public void Dispose()
    {
        foreach (var state in _states.Values)
            lock (state.Lock)
                state.Timer?.Dispose();
        _concurrencyGate.Dispose();
    }

    // Kept alive for the accumulator's whole lifetime, one per group-key — never removed, even
    // after a flush. Simpler than tracking identity-on-removal (what MassTransit's BatchCollector
    // does), at the cost of holding one empty BatchState per distinct key forever. Acceptable
    // trade-off unless a consumer sees unbounded key cardinality (e.g. one key per customer,
    // forever) — flagged as a known limitation, not solved here.
    private sealed class BatchState
    {
        // A plain object lock is used (not System.Threading.Lock) so this still compiles under net8.0.
        public readonly object Lock = new();
        public List<IConsumeContext<TMessage>> Buffer = [];
        public Timer Timer;
    }
}