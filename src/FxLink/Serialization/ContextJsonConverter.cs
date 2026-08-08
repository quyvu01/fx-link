using System.Text.Json;
using System.Text.Json.Serialization;
using FxLink.Abstractions.Contexts;

namespace FxLink.Serialization;

/// <summary>
/// Lets System.Text.Json serialize <see cref="IContext"/>-typed members (e.g. Envelope&lt;TMessage&gt;.Context)
/// by their RUNTIME type instead of the IContext contract. Without this converter, System.Text.Json
/// reflects over the DECLARED type for any member not typed exactly as <c>object</c> — IContext only
/// declares CorrelationId/Headers/SentTime/HostInfo, so every subtype-specific property
/// (PublisherContext.DelayTime, RequestContext.Timeout, ...) would be silently dropped.
/// Write-only: nothing deserializes back into IContext — the receiving side always targets
/// ConsumerContextSerializable instead (see Entities/Envelope.cs).
/// </summary>
internal sealed class ContextJsonConverter : JsonConverter<IContext>
{
    public override IContext Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException(
            $"{nameof(IContext)} is write-only on the wire (see Envelope<TMessage>) — the receiving side " +
            "always deserializes into ConsumerContextSerializable, never back into IContext directly.");

    public override void Write(Utf8JsonWriter writer, IContext value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(IContext), options);
}
