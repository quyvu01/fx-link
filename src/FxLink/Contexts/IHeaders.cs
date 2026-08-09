namespace FxLink.Contexts;

/// <summary>
/// Message headers carried alongside the body. Values set locally are real CLR types; values read
/// after a wire round-trip may arrive as <see cref="System.Text.Json.JsonElement"/> — <see cref="Get{T}"/>
/// transparently handles both.
/// </summary>
public interface IHeaders : IEnumerable<KeyValuePair<string, object>>
{
    bool TryGetHeader(string key, out object value);
    T Get<T>(string key, T defaultValue = default);
    void Set(string key, object value);
}
