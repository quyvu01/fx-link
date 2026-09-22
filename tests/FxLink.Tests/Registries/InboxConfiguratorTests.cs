using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Exceptions;
using FxLink.Extensions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Registries;

public class InboxConfiguratorTests
{
    private interface IInboxWiredMessage
    {
        string Value { get; }
    }

    private sealed record InboxWiredMessage(string Value) : IInboxWiredMessage;

    private sealed class Recorder
    {
        public int InvocationCount;
    }

    private sealed class RecordingConsumer(Recorder recorder) : IConsumer<IInboxWiredMessage>
    {
        public Task ConsumeAsync(IConsumeContext<IInboxWiredMessage> context, CancellationToken token = default)
        {
            Interlocked.Increment(ref recorder.InvocationCount);
            return Task.CompletedTask;
        }
    }

    private static IConsumeContext<IInboxWiredMessage> ContextFor(Guid messageId) =>
        new ConsumeContext<IInboxWiredMessage>(new InboxWiredMessage("a"), new HeaderBag(), Guid.NewGuid(), null,
            DateTime.UtcNow, null, null, messageId);

    [Fact]
    public void UseInbox_registers_default_options_even_when_Options_is_never_called()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts => opts.UseInbox(c => c.InMemoryInbox()));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IInboxOptions>();

        options.ClaimDuration.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Options_configures_the_registered_IInboxOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts => opts.UseInbox(c =>
        {
            c.InMemoryInbox();
            c.Options(x =>
            {
                x.ClaimDuration = TimeSpan.FromSeconds(45);
                x.ClaimRenewInterval = TimeSpan.FromSeconds(15);
            });
        }));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IInboxOptions>();

        options.ClaimDuration.ShouldBe(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void Options_throws_when_ClaimDuration_is_not_positive()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Should.Throw<FxLinkException.InboxOptionsMustBePositive>(() => services.AddFxLink(opts =>
            opts.UseInbox(c => c.Options(x => x.ClaimDuration = TimeSpan.Zero))));
    }

    [Fact]
    public void Options_throws_when_ClaimRenewInterval_is_not_less_than_ClaimDuration()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Should.Throw<FxLinkException.InboxClaimRenewIntervalTooLong>(() => services.AddFxLink(opts =>
            opts.UseInbox(c => c.Options(x =>
            {
                x.ClaimDuration = TimeSpan.FromMinutes(1);
                x.ClaimRenewInterval = TimeSpan.FromMinutes(1);
            }))));
    }

    [Fact]
    public async Task UseInbox_with_InMemoryInbox_dedups_a_redelivered_message_end_to_end()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services.AddFxLink(opts =>
        {
            opts.UseInMemory();
            opts.UseInbox(c => c.InMemoryInbox());
            opts.AddConsumer<RecordingConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var connector = provider.GetRequiredService<IConsumerConnector<IInboxWiredMessage>>();
        var messageId = Guid.NewGuid();

        await connector.ConsumeAsync(ContextFor(messageId), typeof(RecordingConsumer));
        await connector.ConsumeAsync(ContextFor(messageId), typeof(RecordingConsumer));

        var recorder = provider.GetRequiredService<Recorder>();
        recorder.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Without_UseInbox_the_same_message_id_is_processed_every_time()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services.AddFxLink(opts =>
        {
            opts.UseInMemory();
            opts.AddConsumer<RecordingConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var connector = provider.GetRequiredService<IConsumerConnector<IInboxWiredMessage>>();
        var messageId = Guid.NewGuid();

        await connector.ConsumeAsync(ContextFor(messageId), typeof(RecordingConsumer));
        await connector.ConsumeAsync(ContextFor(messageId), typeof(RecordingConsumer));

        var recorder = provider.GetRequiredService<Recorder>();
        recorder.InvocationCount.ShouldBe(2);
    }

    [Fact]
    public async Task MessageInbox_only_dedups_the_message_type_it_was_configured_for()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services.AddFxLink(opts =>
        {
            opts.UseInMemory();
            opts.UseInbox(c => c.MessageInbox<IInboxWiredMessage>(m => m.InMemoryInbox()));
            opts.AddConsumer<RecordingConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var connector = provider.GetRequiredService<IConsumerConnector<IInboxWiredMessage>>();
        var messageId = Guid.NewGuid();

        await connector.ConsumeAsync(ContextFor(messageId), typeof(RecordingConsumer));
        await connector.ConsumeAsync(ContextFor(messageId), typeof(RecordingConsumer));

        var recorder = provider.GetRequiredService<Recorder>();
        recorder.InvocationCount.ShouldBe(1);
    }
}
