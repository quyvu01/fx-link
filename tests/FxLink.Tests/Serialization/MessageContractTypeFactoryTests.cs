using FxLink.Exceptions;
using FxLink.Serialization;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Serialization;

public class MessageContractTypeFactoryTests
{
    public interface IOrderCreatedContract
    {
        Guid OrderId { get; }
        decimal Amount { get; }
    }

    public interface ICorrelated
    {
        Guid CorrelationId { get; }
    }

    public interface IOrderShippedContract : ICorrelated
    {
        Guid OrderId { get; }
    }

    public interface IContractWithMethod
    {
        Guid OrderId { get; }
        void DoSomething();
    }

    public sealed class NotAnInterface
    {
        public Guid OrderId { get; set; }
    }

    [Fact]
    public void GetImplementationType_generates_real_get_and_set_for_getter_only_interface()
    {
        var implementationType = MessageContractTypeFactory.GetImplementationType(typeof(IOrderCreatedContract));

        var instance = Activator.CreateInstance(implementationType)!;
        var orderId = Guid.NewGuid();

        implementationType.GetProperty(nameof(IOrderCreatedContract.OrderId))!.SetValue(instance, orderId);
        implementationType.GetProperty(nameof(IOrderCreatedContract.Amount))!.SetValue(instance, 42m);

        var asInterface = (IOrderCreatedContract)instance;
        asInterface.OrderId.ShouldBe(orderId);
        asInterface.Amount.ShouldBe(42m);
    }

    [Fact]
    public void GetImplementationType_caches_the_same_type_for_repeated_calls()
    {
        var first = MessageContractTypeFactory.GetImplementationType(typeof(IOrderCreatedContract));
        var second = MessageContractTypeFactory.GetImplementationType(typeof(IOrderCreatedContract));

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void GetImplementationType_includes_properties_from_inherited_interfaces()
    {
        var implementationType = MessageContractTypeFactory.GetImplementationType(typeof(IOrderShippedContract));

        implementationType.GetProperty(nameof(IOrderShippedContract.OrderId)).ShouldNotBeNull();
        implementationType.GetProperty(nameof(ICorrelated.CorrelationId)).ShouldNotBeNull();
    }

    [Fact]
    public async Task GetImplementationType_returns_same_type_under_concurrent_first_use()
    {
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => MessageContractTypeFactory.GetImplementationType(typeof(IOrderShippedContract))));

        var results = await Task.WhenAll(tasks);

        results.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public void GetImplementationType_throws_for_a_concrete_class()
    {
        Should.Throw<FxLinkException.MessageContractMustBeInterface>(() =>
            MessageContractTypeFactory.GetImplementationType(typeof(NotAnInterface)));
    }

    [Fact]
    public void GetImplementationType_throws_when_interface_declares_a_method()
    {
        Should.Throw<FxLinkException.MessageContractMustOnlyDeclareProperties>(() =>
            MessageContractTypeFactory.GetImplementationType(typeof(IContractWithMethod)));
    }
}