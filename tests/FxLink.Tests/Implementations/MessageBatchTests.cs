using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Implementations;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Implementations;

public class MessageBatchTests
{
    private sealed record Payload(string Value);

    private static IConsumeContext<Payload> ContextFor(string value) =>
        new ConsumeContext<Payload>(new Payload(value), PublishContext.New(), requesterId: null);

    [Fact]
    public void Length_and_indexer_reflect_the_underlying_messages_in_order()
    {
        var messages = new List<IConsumeContext<Payload>> { ContextFor("a"), ContextFor("b"), ContextFor("c") };

        IBatch<Payload> batch = new MessageBatch<Payload>(messages);

        batch.Length.ShouldBe(3);
        batch[0].Message.Value.ShouldBe("a");
        batch[1].Message.Value.ShouldBe("b");
        batch[2].Message.Value.ShouldBe("c");
    }

    [Fact]
    public void Enumeration_yields_messages_in_the_same_order_as_the_indexer()
    {
        var messages = new List<IConsumeContext<Payload>> { ContextFor("a"), ContextFor("b"), ContextFor("c") };

        IBatch<Payload> batch = new MessageBatch<Payload>(messages);

        var enumerated = batch.Select(x => x.Message.Value).ToArray();
        enumerated.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void Constructor_throws_when_messages_is_null()
    {
        Should.Throw<ArgumentNullException>(() => new MessageBatch<Payload>(null));
    }
}
