using System.Text.Json;
using FxLink.Configurators;
using FxLink.Serialization;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Serialization;

public class InterfaceMessageJsonConverterFactoryTests
{
    public interface IOrderCreatedContract
    {
        Guid OrderId { get; }
        decimal Amount { get; }
    }

    public sealed class NotAnInterface
    {
        public Guid OrderId { get; set; }
    }

    private readonly InterfaceMessageJsonConverterFactory _factory = new();

    [Fact]
    public void CanConvert_is_true_for_a_plain_message_contract_interface() =>
        _factory.CanConvert(typeof(IOrderCreatedContract)).ShouldBeTrue();

    [Fact]
    public void CanConvert_is_false_for_a_concrete_class() =>
        _factory.CanConvert(typeof(NotAnInterface)).ShouldBeFalse();

    [Theory]
    [InlineData(typeof(IEnumerable<int>))]
    [InlineData(typeof(IReadOnlyList<string>))]
    [InlineData(typeof(IDictionary<string, object>))]
    [InlineData(typeof(IDisposable))]
    [InlineData(typeof(IComparable))]
    public void CanConvert_is_false_for_framework_interfaces(Type frameworkInterface) =>
        _factory.CanConvert(frameworkInterface).ShouldBeFalse();

    [Fact]
    public void Serialize_then_deserialize_a_proxy_instance_round_trips_through_the_interface()
    {
        var orderId = Guid.NewGuid();
        var message = MessageContractActivator.CreateFrom<IOrderCreatedContract>(new { OrderId = orderId, Amount = 42m });

        var json = JsonSerializer.Serialize(message, DistributedConfigurators.JsonSerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<IOrderCreatedContract>(json, DistributedConfigurators.JsonSerializerOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.OrderId.ShouldBe(orderId);
        roundTripped.Amount.ShouldBe(42m);
    }
}