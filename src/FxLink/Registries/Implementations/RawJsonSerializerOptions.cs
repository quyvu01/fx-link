using System.Text.Json;
using System.Text.Json.Serialization;

namespace FxLink.Registries;

public sealed class RawJsonSerializerOptions
{
    public JsonNamingPolicy PropertyNamingPolicy { get; set; } = JsonNamingPolicy.CamelCase;
    public bool PropertyNameCaseInsensitive { get; set; } = true;
    public JsonNumberHandling NumberHandling { get; set; }
    public bool AllowTrailingCommas { get; set; }
}
