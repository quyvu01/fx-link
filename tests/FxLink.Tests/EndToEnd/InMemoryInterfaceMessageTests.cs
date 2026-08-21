using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.EndToEnd;

public class InMemoryInterfaceMessageTests
{
    public interface IOrderCreatedContract
    {
        Guid OrderId { get; }
        decimal Amount { get; }
    }

    private sealed class TestMessageRecorder
    {
        private readonly TaskCompletionSource<IOrderCreatedContract> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IOrderCreatedContract> Received => _tcs.Task;
        public void Complete(IOrderCreatedContract message) => _tcs.TrySetResult(message);
    }

    private sealed class RecordingConsumer(TestMessageRecorder recorder) : IConsumer<IOrderCreatedContract>
    {
        public Task ConsumeAsync(IConsumeContext<IOrderCreatedContract> context, CancellationToken token = default)
        {
            recorder.Complete(context.Message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_with_object_values_is_consumed_end_to_end_over_in_memory_transport()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TestMessageRecorder>();
        services.AddFxLink(cfg =>
        {
            cfg.UseInMemory();
            cfg.AddConsumer<RecordingConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();
        var recorder = provider.GetRequiredService<TestMessageRecorder>();

        var orderId = Guid.NewGuid();
        await publisher.PublishAsync<IOrderCreatedContract>(new { OrderId = orderId, Amount = 42m });

        var received = await recorder.Received.WaitAsync(TimeSpan.FromSeconds(5));

        received.OrderId.ShouldBe(orderId);
        received.Amount.ShouldBe(42m);
    }
}