using FxLink.Exceptions;
using FxLink.Serialization;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Serialization;

public class MessageContractActivatorTests
{
    public interface IOrderCreatedContract
    {
        Guid OrderId { get; }
        decimal Amount { get; }
    }

    public sealed class OrderCreatedConcrete
    {
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class OrderCreatedWithoutParameterlessConstructor(Guid orderId)
    {
        public Guid OrderId { get; set; } = orderId;
    }

    public interface IAddressContract
    {
        string City { get; }
        string Street { get; }
    }

    public interface IOrderWithAddressContract
    {
        Guid OrderId { get; }
        IAddressContract ShippingAddress { get; }
    }

    public sealed class AddressConcrete
    {
        public string City { get; set; }
        public string Street { get; set; }
    }

    public interface IOrderWithLineItemsContract
    {
        Guid OrderId { get; }
        IReadOnlyList<ILineItemContract> LineItems { get; }
        string[] Tags { get; }
    }

    public interface ILineItemContract
    {
        string Sku { get; }
        int Quantity { get; }
    }

    public interface IOrderWithItemPricesContract
    {
        Guid OrderId { get; }
        IDictionary<string, decimal> ItemPrices { get; }
        Dictionary<string, ILineItemContract> ItemsBySku { get; }
    }

    public interface IOrderWithNullableAmountContract
    {
        Guid OrderId { get; }
        decimal? Amount { get; }
    }

    [Fact]
    public void CreateFrom_converts_an_int_value_into_a_decimal_target_property()
    {
        var message = MessageContractActivator.CreateFrom<IOrderCreatedContract>(new { OrderId = Guid.NewGuid(), Amount = 123 });

        message.Amount.ShouldBe(123m);
    }

    [Fact]
    public void CreateFrom_converts_a_numeric_string_into_a_decimal_target_property()
    {
        var message = MessageContractActivator.CreateFrom<IOrderCreatedContract>(new { OrderId = Guid.NewGuid(), Amount = "123.45" });

        message.Amount.ShouldBe(123.45m);
    }

    [Fact]
    public void CreateFrom_converts_an_int_value_into_a_nullable_decimal_target_property()
    {
        var message = MessageContractActivator.CreateFrom<IOrderWithNullableAmountContract>(new
        {
            OrderId = Guid.NewGuid(), Amount = 123
        });

        message.Amount.ShouldBe(123m);
    }

    [Fact]
    public void CreateFrom_does_not_convert_a_string_into_a_guid_target_property()
    {
        // Guid does not implement IConvertible — deliberately out of scope for now, unlike
        // MassTransit's dedicated GuidTypeConverter.
        var message = MessageContractActivator.CreateFrom<IOrderCreatedContract>(new
        {
            OrderId = "3fa85f64-5717-4562-b3fc-2c963f66afa6", Amount = 42m
        });

        message.OrderId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void CreateFrom_hydrates_a_dictionary_property_from_a_differently_typed_dictionary()
    {
        var sourcePrices = new Dictionary<string, decimal> { ["sku1"] = 10m, ["sku2"] = 20m };

        var message = MessageContractActivator.CreateFrom<IOrderWithItemPricesContract>(new
        {
            OrderId = Guid.NewGuid(), ItemPrices = sourcePrices
        });

        message.ItemPrices["sku1"].ShouldBe(10m);
        message.ItemPrices["sku2"].ShouldBe(20m);
    }

    [Fact]
    public void CreateFrom_hydrates_a_dictionary_of_nested_contracts_from_dictionary_of_anonymous_values()
    {
        var message = MessageContractActivator.CreateFrom<IOrderWithItemPricesContract>(new
        {
            OrderId = Guid.NewGuid(),
            ItemsBySku = new Dictionary<string, object>
            {
                ["sku1"] = new { Sku = "sku1", Quantity = 3 }
            }
        });

        message.ItemsBySku["sku1"].Sku.ShouldBe("sku1");
        message.ItemsBySku["sku1"].Quantity.ShouldBe(3);
    }

    [Fact]
    public void CreateFrom_skips_dictionary_entries_whose_key_type_does_not_match()
    {
        var sourcePrices = new Dictionary<object, decimal> { ["sku1"] = 10m, [42] = 99m };

        var message = MessageContractActivator.CreateFrom<IOrderWithItemPricesContract>(new
        {
            OrderId = Guid.NewGuid(), ItemPrices = sourcePrices
        });

        message.ItemPrices.Count.ShouldBe(1);
        message.ItemPrices["sku1"].ShouldBe(10m);
    }

    [Fact]
    public void CreateFrom_hydrates_from_a_string_object_dictionary_used_as_a_property_bag()
    {
        var orderId = Guid.NewGuid();

        var message = MessageContractActivator.CreateFrom<IOrderCreatedContract>(new Dictionary<string, object>
        {
            ["OrderId"] = orderId, ["Amount"] = 42m
        });

        message.OrderId.ShouldBe(orderId);
        message.Amount.ShouldBe(42m);
    }

    [Fact]
    public void CreateFrom_property_bag_matches_keys_case_insensitively_and_skips_unknown_keys()
    {
        var orderId = Guid.NewGuid();

        var message = MessageContractActivator.CreateFrom<IOrderCreatedContract>(new Dictionary<string, object>
        {
            ["orderid"] = orderId, ["Amount"] = 42m, ["NotAProperty"] = "ignored"
        });

        message.OrderId.ShouldBe(orderId);
        message.Amount.ShouldBe(42m);
    }

    [Fact]
    public void CreateFrom_property_bag_hydrates_a_nested_contract_value()
    {
        var message = MessageContractActivator.CreateFrom<IOrderWithAddressContract>(new Dictionary<string, object>
        {
            ["OrderId"] = Guid.NewGuid(),
            ["ShippingAddress"] = new { City = "Hanoi", Street = "Ba Trieu" }
        });

        message.ShippingAddress.City.ShouldBe("Hanoi");
        message.ShippingAddress.Street.ShouldBe("Ba Trieu");
    }

    [Fact]
    public void CreateFrom_hydrates_a_nested_interface_property_from_a_differently_typed_source_object()
    {
        var message = MessageContractActivator.CreateFrom<IOrderWithAddressContract>(new
        {
            OrderId = Guid.NewGuid(),
            ShippingAddress = new AddressConcrete { City = "Hanoi", Street = "Ba Trieu" }
        });

        message.ShippingAddress.ShouldNotBeNull();
        message.ShippingAddress.City.ShouldBe("Hanoi");
        message.ShippingAddress.Street.ShouldBe("Ba Trieu");
    }

    [Fact]
    public void CreateFrom_hydrates_a_nested_interface_property_from_an_anonymous_object()
    {
        var message = MessageContractActivator.CreateFrom<IOrderWithAddressContract>(new
        {
            OrderId = Guid.NewGuid(),
            ShippingAddress = new { City = "Hanoi", Street = "Ba Trieu" }
        });

        message.ShippingAddress.City.ShouldBe("Hanoi");
        message.ShippingAddress.Street.ShouldBe("Ba Trieu");
    }

    [Fact]
    public void CreateFrom_sets_null_for_a_nested_property_whose_source_value_is_null()
    {
        var message = MessageContractActivator.CreateFrom<IOrderWithAddressContract>(new
        {
            OrderId = Guid.NewGuid(), ShippingAddress = (AddressConcrete)null
        });

        message.ShippingAddress.ShouldBeNull();
    }

    [Fact]
    public void CreateFrom_hydrates_a_collection_of_nested_objects_and_a_matching_array()
    {
        var message = MessageContractActivator.CreateFrom<IOrderWithLineItemsContract>(new
        {
            OrderId = Guid.NewGuid(),
            LineItems = new[]
            {
                new { Sku = "A1", Quantity = 2 },
                new { Sku = "B2", Quantity = 5 }
            },
            Tags = new[] { "urgent", "gift" }
        });

        message.LineItems.Count.ShouldBe(2);
        message.LineItems[0].Sku.ShouldBe("A1");
        message.LineItems[0].Quantity.ShouldBe(2);
        message.LineItems[1].Sku.ShouldBe("B2");
        message.LineItems[1].Quantity.ShouldBe(5);
        message.Tags.ShouldBe(["urgent", "gift"]);
    }

    [Fact]
    public void CreateFrom_hydrates_interface_contract_from_anonymous_object()
    {
        var orderId = Guid.NewGuid();

        var message =
            MessageContractActivator.CreateFrom<IOrderCreatedContract>(new { OrderId = orderId, Amount = 42m });

        message.OrderId.ShouldBe(orderId);
        message.Amount.ShouldBe(42m);
    }

    [Fact]
    public void CreateFrom_skips_same_named_property_with_a_mismatched_type()
    {
        var message =
            MessageContractActivator.CreateFrom<IOrderCreatedContract>(new { OrderId = "not-a-guid", Amount = 42m });

        message.OrderId.ShouldBe(Guid.Empty);
        message.Amount.ShouldBe(42m);
    }

    [Fact]
    public void CreateFrom_hydrates_concrete_class_with_parameterless_constructor()
    {
        var orderId = Guid.NewGuid();

        var message =
            MessageContractActivator.CreateFrom<OrderCreatedConcrete>(new { OrderId = orderId, Amount = 42m });

        message.OrderId.ShouldBe(orderId);
        message.Amount.ShouldBe(42m);
    }

    [Fact]
    public void CreateFrom_throws_for_concrete_class_without_parameterless_constructor()
    {
        Should.Throw<FxLinkException.MessageContractRequiresParameterlessConstructor>(() =>
            MessageContractActivator.CreateFrom<OrderCreatedWithoutParameterlessConstructor>(new
                { OrderId = Guid.NewGuid() }));
    }
}