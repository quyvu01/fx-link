using FxLink.Contexts;
using FxLink.Registries;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Registries;

public class GroupKeyProviderTests
{
    private sealed record Payload(string Name, int? Priority);

    private static IConsumeContext<Payload> ContextFor(Payload payload) =>
        new ConsumeContext<Payload>(payload, PublishContext.New(), requesterId: null);

    [Fact]
    public void Reference_type_provider_non_generic_escape_hatch_matches_the_typed_result()
    {
        IGroupKeyProvider provider = new GroupKeyProvider<Payload, string>(x => x.Message.Name);
        var context = ContextFor(new Payload("customer-1", null));

        provider.TryGetKey(context, out var key).ShouldBeTrue();
        key.ShouldBe("customer-1");
    }

    [Fact]
    public void Reference_type_provider_non_generic_escape_hatch_returns_false_for_null_selector_result()
    {
        IGroupKeyProvider provider = new GroupKeyProvider<Payload, string>(_ => null);
        var context = ContextFor(new Payload("customer-1", null));

        provider.TryGetKey(context, out var key).ShouldBeFalse();
        key.ShouldBeNull();
    }

    [Fact]
    public void Reference_type_provider_non_generic_escape_hatch_returns_false_for_mismatched_message_type()
    {
        IGroupKeyProvider provider = new GroupKeyProvider<Payload, string>(x => x.Message.Name);
        var wrongContext = new ConsumeContext<string>("not-a-payload", PublishContext.New(), requesterId: null);

        provider.TryGetKey(wrongContext, out var key).ShouldBeFalse();
        key.ShouldBeNull();
    }

    [Fact]
    public void Value_type_provider_non_generic_escape_hatch_matches_the_typed_result()
    {
        IGroupKeyProvider provider = new ValueTypeGroupByProvider<Payload, int>(x => x.Message.Priority);
        var context = ContextFor(new Payload("customer-1", 7));

        provider.TryGetKey(context, out var key).ShouldBeTrue();
        key.ShouldBe(7);
    }

    [Fact]
    public void Value_type_provider_non_generic_escape_hatch_returns_false_when_property_has_no_value()
    {
        IGroupKeyProvider provider = new ValueTypeGroupByProvider<Payload, int>(x => x.Message.Priority);
        var context = ContextFor(new Payload("customer-1", null));

        provider.TryGetKey(context, out var key).ShouldBeFalse();
        key.ShouldBeNull();
    }
}
