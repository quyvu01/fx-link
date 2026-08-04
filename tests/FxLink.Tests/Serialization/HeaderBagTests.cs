using System.Text.Json;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Serialization;

public class HeaderBagTests
{
    [Fact]
    public void Get_returns_exact_match_for_a_live_value()
    {
        var headers = new HeaderBag();
        headers.Set("k", 42);

        headers.Get<int>("k").ShouldBe(42);
    }

    [Fact]
    public void Get_widens_a_live_value_via_convert_change_type()
    {
        var headers = new HeaderBag();
        headers.Set("k", 42);
        headers.Get<long>("k").ShouldBe(42L);

        headers.Set("s", "5");
        headers.Get<int>("s").ShouldBe(5);
    }

    [Fact]
    public void Get_returns_default_for_a_missing_key() =>
        new HeaderBag().Get("missing", -1).ShouldBe(-1);

    [Fact]
    public void Set_with_null_value_removes_the_key()
    {
        var headers = new HeaderBag();
        headers.Set("k", 1);
        headers.Set("k", null);

        headers.TryGetHeader("k", out _).ShouldBeFalse();
    }

    [Fact]
    public void Get_deserializes_a_raw_JsonElement_value()
    {
        var headers = new HeaderBag();
        headers.Set("k", JsonDocument.Parse("42").RootElement);

        headers.Get<int>("k").ShouldBe(42);
    }

    [Fact]
    public void Get_treats_JsonElement_null_kind_as_missing()
    {
        var headers = new HeaderBag();
        headers.Set("k", JsonDocument.Parse("null").RootElement);

        headers.Get("k", "default").ShouldBe("default");
    }

    [Fact]
    public void Keys_are_case_insensitive()
    {
        var headers = new HeaderBag();
        headers.Set("MyKey", 1);

        headers.Get<int>("mykey").ShouldBe(1);
    }

    [Fact]
    public void Header_set_as_live_clr_value_survives_serialize_then_deserialize_then_GetT()
    {
        var correlationId = Guid.NewGuid();
        var headers = new HeaderBag();
        headers.Set(DistributedConfigurators.Headers.RetryCountKey, 3);
        headers.Set("x-delay-ms", 1500.0);
        headers.Set("x-correlation", correlationId);

        var json = JsonSerializer.Serialize<IHeaders>(headers, DistributedConfigurators.JsonSerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<IHeaders>(json, DistributedConfigurators.JsonSerializerOptions);

        roundTripped.Get<int>(DistributedConfigurators.Headers.RetryCountKey).ShouldBe(3);
        roundTripped.Get<double>("x-delay-ms").ShouldBe(1500.0);
        // Guid isn't IConvertible — this only succeeds via the JsonElement.Deserialize<T> branch,
        // pinning down that the fallback order actually matters.
        roundTripped.Get<Guid>("x-correlation").ShouldBe(correlationId);
    }
}
