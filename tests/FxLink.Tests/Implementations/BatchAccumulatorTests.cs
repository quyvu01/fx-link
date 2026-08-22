using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Implementations;
using FxLink.Registries;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Implementations;

public class BatchAccumulatorTests
{
    private sealed record Payload(string Value);

    private static IConsumeContext<Payload> ContextFor(string value) =>
        new ConsumeContext<Payload>(new Payload(value), PublishContext.New(), requesterId: null);

    private static IConsumeContext<Payload> RetriedContextFor(string value)
    {
        var context = ContextFor(value);
        context.Headers.Set(DistributedConfigurators.Headers.RetryCountKey, 1);
        return context;
    }

    [Fact]
    public async Task Flushes_once_MessageLimit_is_reached_and_starts_a_fresh_batch_afterwards()
    {
        var flushed = new List<IReadOnlyList<IConsumeContext<Payload>>>();
        var flushedOnce = new TaskCompletionSource();
        var config = new MessageBatchConfigurator(null, MessageLimit: 3, ConcurrentLimit: 1,
            TimeLimit: TimeSpan.FromMinutes(5), TimeLimitStart: BatchTimeLimitStart.FromFirst);

        var accumulator = new BatchAccumulator<Payload>(config, (batch, _) =>
        {
            flushed.Add(batch);
            flushedOnce.TrySetResult();
            return Task.CompletedTask;
        });

        await accumulator.AddAsync(ContextFor("a"));
        await accumulator.AddAsync(ContextFor("b"));
        flushed.ShouldBeEmpty();
        await accumulator.AddAsync(ContextFor("c"));

        await flushedOnce.Task.WaitAsync(TimeSpan.FromSeconds(2));

        flushed.Count.ShouldBe(1);
        flushed[0].Select(x => x.Message.Value).ShouldBe(["a", "b", "c"]);

        // Next message starts a brand-new batch, not appended to the flushed one.
        await accumulator.AddAsync(ContextFor("d"));
        await Task.Delay(50);
        flushed.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Flushes_a_partial_batch_once_TimeLimit_elapses()
    {
        var flushed = new List<IReadOnlyList<IConsumeContext<Payload>>>();
        var flushedOnce = new TaskCompletionSource();
        var config = new MessageBatchConfigurator(null, MessageLimit: 100, ConcurrentLimit: 1,
            TimeLimit: TimeSpan.FromMilliseconds(100), TimeLimitStart: BatchTimeLimitStart.FromFirst);

        var accumulator = new BatchAccumulator<Payload>(config, (batch, _) =>
        {
            flushed.Add(batch);
            flushedOnce.TrySetResult();
            return Task.CompletedTask;
        });

        await accumulator.AddAsync(ContextFor("a"));
        await accumulator.AddAsync(ContextFor("b"));

        await flushedOnce.Task.WaitAsync(TimeSpan.FromSeconds(2));

        flushed.Count.ShouldBe(1);
        flushed[0].Select(x => x.Message.Value).ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task FromLast_resets_the_timer_on_every_message_delaying_flush_until_idle()
    {
        var flushed = new List<IReadOnlyList<IConsumeContext<Payload>>>();
        var flushedOnce = new TaskCompletionSource();
        var config = new MessageBatchConfigurator(null, MessageLimit: 100, ConcurrentLimit: 1,
            TimeLimit: TimeSpan.FromMilliseconds(250), TimeLimitStart: BatchTimeLimitStart.FromLast);

        var accumulator = new BatchAccumulator<Payload>(config, (batch, _) =>
        {
            flushed.Add(batch);
            flushedOnce.TrySetResult();
            return Task.CompletedTask;
        });

        await accumulator.AddAsync(ContextFor("a"));
        await Task.Delay(120);
        await accumulator.AddAsync(ContextFor("b")); // resets the 250ms clock

        // 150ms after the second add = 270ms after the first add — past the ORIGINAL (unreset)
        // deadline, but before the reset one. If FromLast weren't resetting, this would already
        // have flushed by now.
        await Task.Delay(150);
        flushed.ShouldBeEmpty();

        await flushedOnce.Task.WaitAsync(TimeSpan.FromSeconds(2));
        flushed[0].Select(x => x.Message.Value).ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task GroupBy_routes_messages_with_different_keys_into_independent_batches()
    {
        var flushed = new System.Collections.Concurrent.ConcurrentBag<IReadOnlyList<IConsumeContext<Payload>>>();
        var flushCount = 0;
        var bothFlushed = new TaskCompletionSource();
        var provider = new GroupKeyProvider<Payload, string>(x => x.Message.Value);
        var config = new MessageBatchConfigurator(provider, MessageLimit: 1, ConcurrentLimit: 2,
            TimeLimit: TimeSpan.FromMinutes(5), TimeLimitStart: BatchTimeLimitStart.FromFirst);

        var accumulator = new BatchAccumulator<Payload>(config, (batch, _) =>
        {
            flushed.Add(batch);
            if (Interlocked.Increment(ref flushCount) == 2) bothFlushed.TrySetResult();
            return Task.CompletedTask;
        });

        await accumulator.AddAsync(ContextFor("group-a"));
        await accumulator.AddAsync(ContextFor("group-b"));

        await bothFlushed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        flushed.Count.ShouldBe(2);
        flushed.ShouldContain(b => b.Count == 1 && b[0].Message.Value == "group-a");
        flushed.ShouldContain(b => b.Count == 1 && b[0].Message.Value == "group-b");
    }

    [Fact]
    public async Task Messages_that_cannot_be_grouped_fall_into_a_shared_ungrouped_batch()
    {
        var flushed = new List<IReadOnlyList<IConsumeContext<Payload>>>();
        var flushedOnce = new TaskCompletionSource();
        var provider = new GroupKeyProvider<Payload, string>(x => x.Message.Value == "known" ? "known-key" : null);
        var config = new MessageBatchConfigurator(provider, MessageLimit: 2, ConcurrentLimit: 1,
            TimeLimit: TimeSpan.FromMinutes(5), TimeLimitStart: BatchTimeLimitStart.FromFirst);

        var accumulator = new BatchAccumulator<Payload>(config, (batch, _) =>
        {
            flushed.Add(batch);
            flushedOnce.TrySetResult();
            return Task.CompletedTask;
        });

        await accumulator.AddAsync(ContextFor("other1"));
        await accumulator.AddAsync(ContextFor("other2")); // same ungrouped bucket -> reaches MessageLimit(2)

        await flushedOnce.Task.WaitAsync(TimeSpan.FromSeconds(2));

        flushed.Count.ShouldBe(1);
        flushed[0].Select(x => x.Message.Value).ShouldBe(["other1", "other2"]);
    }

    [Fact]
    public async Task A_message_with_a_nonzero_retry_count_forces_an_immediate_flush_below_MessageLimit()
    {
        var flushed = new List<IReadOnlyList<IConsumeContext<Payload>>>();
        var flushedOnce = new TaskCompletionSource();
        var config = new MessageBatchConfigurator(null, MessageLimit: 100, ConcurrentLimit: 1,
            TimeLimit: TimeSpan.FromMinutes(5), TimeLimitStart: BatchTimeLimitStart.FromFirst);

        var accumulator = new BatchAccumulator<Payload>(config, (batch, _) =>
        {
            flushed.Add(batch);
            flushedOnce.TrySetResult();
            return Task.CompletedTask;
        });

        await accumulator.AddAsync(RetriedContextFor("a"));

        await flushedOnce.Task.WaitAsync(TimeSpan.FromSeconds(2));

        flushed.Count.ShouldBe(1);
        flushed[0].Select(x => x.Message.Value).ShouldBe(["a"]);
    }

    [Fact]
    public async Task ConcurrencyLimit_serializes_flush_handler_invocations()
    {
        var firstEntered = new TaskCompletionSource();
        var releaseFirst = new TaskCompletionSource();
        var secondEntered = new TaskCompletionSource();
        var callCount = 0;
        var provider = new GroupKeyProvider<Payload, string>(x => x.Message.Value);
        var config = new MessageBatchConfigurator(provider, MessageLimit: 1, ConcurrentLimit: 1,
            TimeLimit: TimeSpan.FromMinutes(5), TimeLimitStart: BatchTimeLimitStart.FromFirst);

        var accumulator = new BatchAccumulator<Payload>(config, async (_, _) =>
        {
            if (Interlocked.Increment(ref callCount) == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task;
            }
            else
            {
                secondEntered.TrySetResult();
            }
        });

        await accumulator.AddAsync(ContextFor("group-a"));
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await accumulator.AddAsync(ContextFor("group-b"));

        var wonEarly = await Task.WhenAny(secondEntered.Task, Task.Delay(300));
        wonEarly.ShouldNotBe(secondEntered.Task);

        releaseFirst.TrySetResult();
        await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
