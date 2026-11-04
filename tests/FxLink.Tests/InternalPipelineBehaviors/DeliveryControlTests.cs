using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Extensions;
using FxLink.Statics;
using Shouldly;
using Xunit;

namespace FxLink.Tests.InternalPipelineBehaviors;

public class DeliveryControlTests
{
    private sealed record Payload(string Value);

    private static ConsumeContext<Payload> RetriedContext(string deliveryKind, int retryCount)
    {
        var headers = new HeaderBag();
        headers.Set(DistributedConfigurators.Headers.RetryCountKey, retryCount);
        headers.Set(DistributedConfigurators.Headers.DeliveryKindKey, deliveryKind);
        return new ConsumeContext<Payload>(new Payload("p"), headers, Id.New(), requesterId: null);
    }

    [Theory]
    [InlineData(DistributedConfigurators.DeliveryKinds.Retry)]
    [InlineData(DistributedConfigurators.DeliveryKinds.DeadLetter)]
    public void Control_headers_do_not_reach_messages_derived_from_a_retried_delivery(string deliveryKind)
    {
        var context = RetriedContext(deliveryKind, retryCount: 2);

        context.CaptureDeliveryControl();

        var derived = new PublishContext(context);
        derived.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey).ShouldBeNull();
        derived.Headers.Get<int>(DistributedConfigurators.Headers.RetryCountKey).ShouldBe(0);
    }

    [Fact]
    public void The_retry_count_is_still_available_to_the_retry_behaviors_after_capture()
    {
        var context = RetriedContext(DistributedConfigurators.DeliveryKinds.Retry, retryCount: 2);

        context.CaptureDeliveryControl();

        context.GetRetryCount().ShouldBe(2);
    }

    [Fact]
    public void A_delayed_delivery_keeps_its_delay_kind_because_state_machines_read_it()
    {
        var context = RetriedContext(DistributedConfigurators.DeliveryKinds.Delay, retryCount: 0);

        context.CaptureDeliveryControl();

        context.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey)
            .ShouldBe(DistributedConfigurators.DeliveryKinds.Delay);
    }

    [Fact]
    public void Without_a_capture_the_retry_count_falls_back_to_the_header()
    {
        RetriedContext(DistributedConfigurators.DeliveryKinds.Retry, retryCount: 3).GetRetryCount().ShouldBe(3);
    }
}
