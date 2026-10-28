using FxLink.Abstractions;
using FxLink.Implementations;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Implementations;

public class MessageTypeResolverTests
{
    public sealed record PlainMessage(string Value);

    public sealed record NamedMessage(string Value);

    public sealed record Envelope<T>(T Value);

    private sealed record NotConsumed(string Value);

    private sealed class NamedMessageDefinition : MessageDefinition<NamedMessage>
    {
        public override void Configure(IMessageConfigurator<NamedMessage> options) =>
            options.Name("orders.named-message");
    }

    private static MessageTypeResolver CreateResolver()
    {
        var messageKeys = new MessageKeys();
        messageKeys.AddMessageKey(typeof(PlainMessage), typeof(object));
        messageKeys.AddMessageKey(typeof(NamedMessage), typeof(object));
        messageKeys.AddMessageKey(typeof(Envelope<PlainMessage>), typeof(object));

        var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageDefinition<NamedMessage>>(new NamedMessageDefinition())
            .BuildServiceProvider();
        return new MessageTypeResolver(messageKeys, serviceProvider);
    }

    [Fact]
    public void Resolves_a_consumed_type_by_its_assembly_qualified_name()
    {
        CreateResolver().Resolve(typeof(PlainMessage).AssemblyQualifiedName).ShouldBe(typeof(PlainMessage));
    }

    [Fact]
    public void Resolves_a_consumed_type_sent_from_a_different_contracts_assembly_version()
    {
        var name = typeof(PlainMessage).AssemblyQualifiedName!
            .Replace(typeof(PlainMessage).Assembly.GetName().Version!.ToString(), "9.8.7.6");

        CreateResolver().Resolve(name).ShouldBe(typeof(PlainMessage));
    }

    [Fact]
    public void Resolves_a_closed_generic_whose_type_argument_names_carry_their_own_version_details()
    {
        var name = typeof(Envelope<PlainMessage>).AssemblyQualifiedName!
            .Replace(typeof(PlainMessage).Assembly.GetName().Version!.ToString(), "9.8.7.6");

        CreateResolver().Resolve(name).ShouldBe(typeof(Envelope<PlainMessage>));
    }

    [Fact]
    public void Resolves_a_consumed_type_by_its_custom_message_definition_name()
    {
        var resolver = CreateResolver();

        resolver.Resolve("orders.named-message").ShouldBe(typeof(NamedMessage));
        resolver.Resolve(typeof(NamedMessage).AssemblyQualifiedName).ShouldBe(typeof(NamedMessage));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a type name")]
    [InlineData("System.Diagnostics.Process, System.Diagnostics.Process")]
    public void Never_resolves_a_name_outside_the_consumed_message_types(string name)
    {
        CreateResolver().Resolve(name).ShouldBeNull();
    }

    [Fact]
    public void Never_resolves_a_loadable_type_this_process_does_not_consume()
    {
        CreateResolver().Resolve(typeof(NotConsumed).AssemblyQualifiedName).ShouldBeNull();
    }
}
