using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.Implementations;
using FxLink.Wrappers;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Implementations;

public class InMemoryResponseProcessorTests
{
    public sealed record Ping(string Value);

    public sealed record Pong(string Value);

    private static MessageData<Result<Pong>> Reply(string value, IContext context) =>
        new(Result<Pong>.Success(new Pong(value)), context, CancellationToken.None);

    // Answers inside SendAsync, i.e. before the requester's send has returned — the fastest a real reply
    // can come back (a responder replying while the requester still waits for its publisher confirm).
    private sealed class ImmediatelyReplyingConnector(IInMemoryResponseSetter setter) : IClientConnector<Ping>
    {
        public Task SendAsync(Ping message, IContext context, CancellationToken token = default)
        {
            var requesterId = ((IRequestContext)context).RequesterId;
            setter.TrySetResult(requesterId, Reply(message.Value, context)).ShouldBeTrue();
            return Task.CompletedTask;
        }
    }

    private sealed class FailingConnector : IClientConnector<Ping>
    {
        public Task SendAsync(Ping message, IContext context, CancellationToken token = default) =>
            throw new InvalidOperationException("broker down");
    }

    [Fact]
    public async Task A_reply_that_arrives_before_the_send_returns_still_completes_the_request()
    {
        var processor = new InMemoryResponseProcessor();
        var requester = new Requester<Ping>(new ImmediatelyReplyingConnector(processor), processor);

        var response = await requester.RequestAsync<Pong>(new Ping("hello"), ctx => ctx.Timeout = TimeSpan.FromSeconds(5));

        response.Message.Value.ShouldBe("hello");
    }

    [Fact]
    public async Task A_failed_send_surfaces_its_own_exception_and_leaves_nothing_pending()
    {
        var processor = new InMemoryResponseProcessor();
        var requester = new Requester<Ping>(new FailingConnector(), processor);
        Guid requesterId = default;

        await Should.ThrowAsync<InvalidOperationException>(() => requester.RequestAsync<Pong>(new Ping("x"),
            ctx =>
            {
                ctx.Timeout = TimeSpan.FromSeconds(30);
                requesterId = ctx.RequesterId;
            }));

        processor.TrySetResult(requesterId, Reply("late", null)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_completed_or_timed_out_request_is_no_longer_tracked()
    {
        var processor = new InMemoryResponseProcessor();

        var completedId = Guid.NewGuid();
        var completed = processor.GetResponse<Pong>(completedId);
        processor.TrySetResult(completedId, Reply("done", null)).ShouldBeTrue();
        (await completed).Message.Data.Value.ShouldBe("done");
        processor.TrySetResult(completedId, Reply("again", null)).ShouldBeFalse();

        var timedOutId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var timedOut = processor.GetResponse<Pong>(timedOutId, cts.Token);
        await cts.CancelAsync();
        await Should.ThrowAsync<TimeoutException>(() => timedOut);
        processor.TrySetResult(timedOutId, Reply("late", null)).ShouldBeFalse();
    }

    [Fact]
    public void Resolves_a_reply_type_only_once_a_request_waits_on_that_response_type()
    {
        var processor = new InMemoryResponseProcessor();
        var wireName = typeof(Result<Pong>).AssemblyQualifiedName!;

        processor.ResolveResultType(wireName).ShouldBeNull();

        _ = processor.GetResponse<Pong>(Guid.NewGuid());

        processor.ResolveResultType(wireName).ShouldBe(typeof(Result<Pong>));
        processor.ResolveResultType(wireName.Replace(typeof(Pong).Assembly.GetName().Version!.ToString(), "9.8.7.6"))
            .ShouldBe(typeof(Result<Pong>));
        processor.ResolveResultType(typeof(Result<Ping>).AssemblyQualifiedName).ShouldBeNull();
        processor.ResolveResultType("System.Diagnostics.Process, System.Diagnostics.Process").ShouldBeNull();
        processor.ResolveResultType(null).ShouldBeNull();
    }
}
