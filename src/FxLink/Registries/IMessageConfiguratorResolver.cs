using System.Text.Json;

namespace FxLink.Registries;

public interface IMessageConfiguratorResolver
{
    string GetName();
    bool IsRawJsonSerializer { get; }
    JsonSerializerOptions RawJsonSerializerOptions { get; }
}