using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.InternalPipelineBehaviors;
using FxLink.Statics;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.InternalPipelineBehaviors;

public class RequestFaultPipelineBehaviorTests
{
    private sealed record Payload(string Value);

    private static ConsumeContext<Payload> BuildContext(bool isRequest)
    {
        var headers = new HeaderBag();
        if (isRequest)
            headers.Set(DistributedConfigurators.Headers.RequestSemanticsKey,
                DistributedConfigurators.RequestSemantics.RequestAsPublisher);
        var context = new ConsumeContext<Payload>(new Payload("p"), headers, Id.New(), requesterId: null);
        context.SetPayload<IServiceProvider>(new ServiceCollection().BuildServiceProvider());
        return context;
    }

    private static ConsumerHandlerDelegate Throws(Exception ex) => _ => throw ex;

    [Fact]
    public async Task A_failing_request_is_absorbed_so_it_is_neither_retried_nor_rethrown_to_the_transport()
    {
        var behavior = new RequestFaultPipelineBehavior<Payload>();

        await behavior.ConsumeAsync(BuildContext(isRequest: true), Throws(new InvalidOperationException("boom")));
    }

    [Fact]
    public async Task A_failing_regular_message_still_propagates_to_the_retry_behavior()
    {
        var behavior = new RequestFaultPipelineBehavior<Payload>();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            behavior.ConsumeAsync(BuildContext(isRequest: false), Throws(new InvalidOperationException("boom"))));
    }

    [Fact]
    public async Task A_succeeding_request_just_runs_the_rest_of_the_pipeline()
    {
        var behavior = new RequestFaultPipelineBehavior<Payload>();
        var called = false;

        await behavior.ConsumeAsync(BuildContext(isRequest: true), _ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        called.ShouldBeTrue();
    }
}
