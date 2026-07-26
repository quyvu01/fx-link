using FxLink.Wrappers;
using RabbitMQ.Client;

namespace FxLink.RabbitMq.Implementations;

/// <summary>
/// Wraps a single <see cref="IChannel"/> for use with <see cref="Recycle{T}"/>: the channel (and
/// whatever setup callback declared on it — queues, consumers, ...) is created lazily on first
/// <see cref="GetChannelAsync"/> call, and <see cref="Stopping"/> completes the moment the broker
/// or client closes this specific channel, independently of the connection's own lifecycle.
/// </summary>
internal sealed class RecyclableChannel : IRecyclable
{
    private readonly IConnection _connection;
    private readonly Func<IChannel, Task> _setupAsync;
    private readonly CancellationToken _cancellationToken;
    private readonly CreateChannelOptions _createChannelOptions;
    private readonly TaskCompletionSource _stopping = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lazy<Task<IChannel>> _channelTask;

    public RecyclableChannel(IConnection connection, Func<IChannel, Task> setupAsync, CancellationToken cancellationToken,
        CreateChannelOptions createChannelOptions = null)
    {
        _connection = connection;
        _setupAsync = setupAsync;
        _cancellationToken = cancellationToken;
        _createChannelOptions = createChannelOptions;
        _channelTask = new Lazy<Task<IChannel>>(CreateAsync);
    }

    public Task Stopping => _stopping.Task;

    public Task<IChannel> GetChannelAsync() => _channelTask.Value;

    private async Task<IChannel> CreateAsync()
    {
        var channel = await _connection.CreateChannelAsync(_createChannelOptions, _cancellationToken);
        channel.ChannelShutdownAsync += (_, _) =>
        {
            _stopping.TrySetResult();
            return Task.CompletedTask;
        };
        await _setupAsync(channel);
        return channel;
    }
}
