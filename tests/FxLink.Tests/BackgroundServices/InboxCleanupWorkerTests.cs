using FxLink.Abstractions;
using FxLink.BackgroundServices;
using FxLink.InMemory;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace FxLink.Tests.BackgroundServices;

public class InboxCleanupWorkerTests
{
    private const string ConsumerKey = "SomeConsumer";

    private static InboxOptions FastOptions => new()
    {
        RetentionPeriod = TimeSpan.FromMilliseconds(50), CleanupInterval = TimeSpan.FromMilliseconds(30)
    };

    private static IServiceProvider BuildProvider(IInboxStore inboxStore)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(inboxStore);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Deletes_processed_records_once_they_exceed_the_retention_period()
    {
        var inboxStore = new InMemoryInboxStore();
        var registry = new InboxRegistry();
        registry.RegisterDefault();

        var processedId = Guid.NewGuid();
        var claim = await inboxStore.TryClaimAsync(ConsumerKey, processedId, TimeSpan.FromSeconds(30));
        await inboxStore.MarkProcessedAsync(ConsumerKey, processedId, claim!.Value);

        var stillClaimedId = Guid.NewGuid();
        await inboxStore.TryClaimAsync(ConsumerKey, stillClaimedId, TimeSpan.FromSeconds(30));

        var provider = BuildProvider(inboxStore);
        var worker = new InboxCleanupWorker(registry, provider,
            provider.GetRequiredService<ILogger<InboxCleanupWorker>>(), FastOptions);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(300);
        await worker.StopAsync(CancellationToken.None);

        inboxStore.Contains(ConsumerKey, processedId).ShouldBeFalse();
        // Claimed (not yet Processed) records are never swept by retention — only expiry/steal or a
        // successful MarkProcessedAsync ever removes/transitions one.
        inboxStore.Contains(ConsumerKey, stillClaimedId).ShouldBeTrue();
    }

    [Fact]
    public async Task Leaves_recently_processed_records_alone_until_the_retention_period_elapses()
    {
        var inboxStore = new InMemoryInboxStore();
        var registry = new InboxRegistry();
        registry.RegisterDefault();

        var processedId = Guid.NewGuid();
        var claim = await inboxStore.TryClaimAsync(ConsumerKey, processedId, TimeSpan.FromSeconds(30));
        await inboxStore.MarkProcessedAsync(ConsumerKey, processedId, claim!.Value);

        var provider = BuildProvider(inboxStore);
        var longRetention = new InboxOptions
        {
            RetentionPeriod = TimeSpan.FromHours(1), CleanupInterval = TimeSpan.FromMilliseconds(30)
        };
        var worker = new InboxCleanupWorker(registry, provider,
            provider.GetRequiredService<ILogger<InboxCleanupWorker>>(), longRetention);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await worker.StopAsync(CancellationToken.None);

        inboxStore.Contains(ConsumerKey, processedId).ShouldBeTrue();
    }
}
