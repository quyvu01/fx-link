using System.Text.Json;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Serialization;

public class ContextJsonConverterTests
{
    private sealed record Payload(string Value);

    [Fact]
    public void Envelope_serializes_IContext_by_runtime_type_not_the_4_member_contract()
    {
        var context = PublishContext.New();
        context.DelayTime = TimeSpan.FromSeconds(30);
        var envelope = new Envelope<Payload>(new Payload("x"), context);

        var json = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        using var doc = JsonDocument.Parse(json);
        var contextElement = doc.RootElement.GetProperty("context");

        // delayTime only exists on PublisherContext, not on the IContext contract — if this converter
        // regresses back to reflecting over IContext's 4 declared members, this property disappears.
        contextElement.TryGetProperty("delayTime", out var delayTime).ShouldBeTrue();
        delayTime.GetString().ShouldBe("00:00:30");
    }

    [Fact]
    public void Envelope_deserialize_throws_because_IContext_is_write_only_on_the_wire()
    {
        var envelope = new Envelope<Payload>(new Payload("x"), PublishContext.New());
        var json = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);

        Should.Throw<NotSupportedException>(() =>
            JsonSerializer.Deserialize<Envelope<Payload>>(json, DistributedConfigurators.JsonSerializerOptions));
    }

    [Fact]
    public void HostInfo_round_trips_through_JSON_now_that_its_accessors_are_public_init()
    {
        var original = (HostInfo)HostInfo.Current;

        var json = JsonSerializer.Serialize(original, DistributedConfigurators.JsonSerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<HostInfo>(json, DistributedConfigurators.JsonSerializerOptions);

        // Regression guard: with `private init` accessors, JsonSerializer.Deserialize has no way to
        // set these — every field would silently come back null/0 regardless of what was sent.
        roundTripped.MachineName.ShouldBe(original.MachineName);
        roundTripped.ProcessName.ShouldBe(original.ProcessName);
        roundTripped.ProcessId.ShouldBe(original.ProcessId);
        roundTripped.Assembly.ShouldBe(original.Assembly);
    }

    [Fact]
    public void ConsumerContext_preserves_the_original_SentTime_HostInfo_and_TimeToLive_instead_of_generating_fresh_ones()
    {
        var senderContext = PublishContext.New();
        senderContext.TimeToLive = TimeSpan.FromSeconds(5);
        var envelope = new Envelope<Payload>(new Payload("x"), senderContext);

        var json = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        var received = JsonSerializer.Deserialize<ConsumerContextEnvelope<Payload>>(json,
            DistributedConfigurators.JsonSerializerOptions);

        var consumerContext = new ConsumeContext<Payload>(received.Message, received.Context.Headers,
            received.Context.CorrelationId, received.Context.RequesterId,
            received.Context.SentTime, received.Context.HostInfo);

        consumerContext.SentTime.ShouldBe(senderContext.SentTime);
        consumerContext.HostInfo.MachineName.ShouldBe(senderContext.HostInfo.MachineName);
        consumerContext.HostInfo.ProcessId.ShouldBe(senderContext.HostInfo.ProcessId);
        received.Context.TimeToLive.ShouldBe(TimeSpan.FromSeconds(5));
    }
}
