using FxLink.BackgroundServices;
using FxLink.Extensions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Registries;

public class OutboxConfiguratorTests
{
    [Fact]
    public void UseOutbox_registers_default_dispatcher_options_even_when_DispatcherOptions_is_never_called()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts => opts.UseOutbox(c => c.InMemoryOutbox()));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOutboxDispatcherOptions>();

        options.PollInterval.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void DispatcherOptions_configures_the_registered_IOutboxDispatcherOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts => opts.UseOutbox(c =>
        {
            c.InMemoryOutbox();
            c.DispatcherOptions(x => x.PollInterval = TimeSpan.FromMilliseconds(500));
        }));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOutboxDispatcherOptions>();

        options.PollInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void UseOutbox_registers_the_dispatcher_and_cleanup_hosted_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts => opts.UseOutbox(c => c.InMemoryOutbox()));

        using var provider = services.BuildServiceProvider();
        var hostedServiceNames = provider.GetServices<IHostedService>().Select(s => s.GetType().Name).ToList();

        hostedServiceNames.ShouldContain(nameof(OutboxDispatcherWorker));
        hostedServiceNames.ShouldContain(nameof(OutboxCleanupWorker));
    }
}
