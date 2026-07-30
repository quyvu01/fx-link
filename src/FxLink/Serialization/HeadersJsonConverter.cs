using System.Text.Json;
using System.Text.Json.Serialization;
using FxLink.Abstractions.Contexts;

namespace FxLink.Serialization;

/// <summary>
/// Lets System.Text.Json (de)serialize <see cref="IHeaders"/>. Without this converter, a property
/// declared as an interface type serializes by its declared type's public members — <see cref="IHeaders"/>
/// has none (only methods) — so it would otherwise serialize as an empty object.
/// </summary>
internal sealed class HeadersJsonConverter : JsonConverter<IHeaders>
{
    public override IHeaders Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
        return new HeaderBag(dictionary ?? new Dictionary<string, object>());
    }

    public override void Write(Utf8JsonWriter writer, IHeaders value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, headerValue) in value)
        {
            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, headerValue, headerValue?.GetType() ?? typeof(object), options);
        }

        writer.WriteEndObject();
    }
}
