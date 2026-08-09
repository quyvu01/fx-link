using FxLink.Contexts;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Contexts;

public class ResponseContextTests
{
    private sealed record Payload(string Value);

    [Fact]
    public void ResponseContext_carries_forward_TimeToLive_from_the_consumed_request()
    {
        var requestTimeToLive = TimeSpan.FromSeconds(7);
        var consumedRequest = new ConsumerContext<Payload>(new Payload("x"), requesterId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(), headers: new HeaderBag(), sentTime: null, hostInfo: null,
            timeToLive: requestTimeToLive);

        var responseContext = new ResponseContext(Guid.NewGuid(), consumedRequest);

        responseContext.TimeToLive.ShouldBe(requestTimeToLive);
    }

    [Fact]
    public void ResponseContext_TimeToLive_is_null_when_the_source_context_is_not_a_consumer_context()
    {
        var publisherContext = PublisherContext.New();

        var responseContext = new ResponseContext(Guid.NewGuid(), publisherContext);

        responseContext.TimeToLive.ShouldBeNull();
    }
}
