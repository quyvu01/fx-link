using System.Collections;
using System.Text.Json;
using FxLink.Configurators;
using FxLink.Serialization;

namespace FxLink.Contexts;

internal sealed class HeaderBag : IHeaders
{
    private readonly Dictionary<string, object> _headers = new(StringComparer.OrdinalIgnoreCase);

    public HeaderBag()
    {
    }

    public HeaderBag(IEnumerable<KeyValuePair<string, object>> source)
    {
        foreach (var (key, value) in source) Set(key, value);
    }

    public void Set(string key, object value)
    {
        if (value is null) _headers.Remove(key);
        else _headers[key] = value;
    }

    public bool TryGetHeader(string key, out object value) => _headers.TryGetValue(key, out value);

    public T Get<T>(string key, T defaultValue = default)
    {
        if (!_headers.TryGetValue(key, out var value) || value is null ||
            value is JsonElement { ValueKind: JsonValueKind.Null }) return defaultValue;

        switch (value)
        {
            case T typed:
                return typed;
            case JsonElement element:
                try
                {
                    return element.Deserialize<T>(DistributedConfigurators.JsonSerializerOptions);
                }
                catch
                {
                    return defaultValue;
                }

            default:
                return MessageContractActivator.TryConvertPrimitive(typeof(T), value, out var converted)
                    ? (T)converted
                    : defaultValue;
        }
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _headers.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}