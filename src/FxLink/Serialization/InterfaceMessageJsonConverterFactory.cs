using System.Text.Json;
using System.Text.Json.Serialization;

namespace FxLink.Serialization;

/// <summary>
/// Lets System.Text.Json deserialize a bare message-contract interface by routing it through the
/// Reflection.Emit-generated concrete implementation from <see cref="MessageContractTypeFactory"/>.
/// <see cref="CanConvert"/>'s scoping is safety-critical: this factory is registered globally on
/// the shared <see cref="Configurators.DistributedConfigurators.JsonSerializerOptions"/>, so it
/// must reject framework interfaces (IEnumerable&lt;T&gt;, IDictionary&lt;K,V&gt;, IDisposable, etc.) or it
/// would hijack serialization anywhere those appear, not just FxLink message envelopes.
/// </summary>
internal sealed class InterfaceMessageJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsInterface && IsEligibleMessageContract(typeToConvert);

    // Mirrors MassTransit's MessageTypeCache.CheckIfValidMessageType exclusions: reject types with
    // no/System namespace and types defined in the same assembly as System.Object (corlib) — this
    // is what keeps IEnumerable<T>/IDictionary<K,V>/IDisposable/IComparable etc. out of this path.
    private static bool IsEligibleMessageContract(Type type) =>
        type.Namespace is { Length: > 0 } ns
        && ns != nameof(System) && !ns.StartsWith($"{nameof(System)}.", StringComparison.Ordinal)
        && type.Assembly != typeof(object).Assembly;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var implementationType = MessageContractTypeFactory.GetImplementationType(typeToConvert);
        var converterType = typeof(InterfaceMessageJsonConverter<,>).MakeGenericType(typeToConvert, implementationType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal sealed class InterfaceMessageJsonConverter<TInterface, TImplementation> : JsonConverter<TInterface>
    where TImplementation : TInterface
{
    public override TInterface Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<TImplementation>(ref reader, options)!;

    public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options) =>
        // Serialize using the RUNTIME type, not TInterface — avoids re-entering this same
        // converter (CanConvert(TInterface) would match again) and captures the real values.
        JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(TInterface), options);
}