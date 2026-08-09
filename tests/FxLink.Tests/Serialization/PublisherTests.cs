using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Serialization;

public class PublisherTests
{
    public interface IOrderCreatedContract
    {
        Guid OrderId { get; }
        decimal Amount { get; }
    }

    private sealed class FakeClientConnector : IClientConnector<IOrderCreatedContract>
    {
        public IOrderCreatedContract Received { get; private set; }

        public Task SendAsync(IOrderCreatedContract message, IContext context, CancellationToken token = default)
        {
            Received = message;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_with_object_values_hydrates_and_forwards_to_the_typed_overload()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(_ => { });
        var fakeConnector = new FakeClientConnector();
        services.AddSingleton<IClientConnector<IOrderCreatedContract>>(fakeConnector);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        var orderId = Guid.NewGuid();
        await publisher.PublishAsync<IOrderCreatedContract>(new { OrderId = orderId, Amount = 42m });

        fakeConnector.Received.ShouldNotBeNull();
        fakeConnector.Received.OrderId.ShouldBe(orderId);
        fakeConnector.Received.Amount.ShouldBe(42m);
    }
}