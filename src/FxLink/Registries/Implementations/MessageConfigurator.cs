using System.Text.Json;
using FxLink.Configurators;

namespace FxLink.Registries;

internal class MessageConfigurator<TMessage> : IMessageConfigurator<TMessage>, IMessageConfiguratorResolver
    where TMessage : class
{
    private string _messageName;
    private Action<RawJsonSerializerOptions> _rawJsonConfigure;

    public string GetName() => _messageName;
    public bool IsRawJsonSerializer { get; private set; }
    public void Name(string name) => _messageName = name;

    public void UseRawJsonSerializer(Action<RawJsonSerializerOptions> configure = null)
    {
        IsRawJsonSerializer = true;
        _rawJsonConfigure = configure;
    }

    public JsonSerializerOptions RawJsonSerializerOptions => BuildRawJsonSerializerOptions();

    private JsonSerializerOptions BuildRawJsonSerializerOptions()
    {
        if (_rawJsonConfigure is null) return DistributedConfigurators.JsonSerializerOptions;

        var options = new RawJsonSerializerOptions();
        _rawJsonConfigure.Invoke(options);

        return new JsonSerializerOptions(DistributedConfigurators.JsonSerializerOptions)
        {
            PropertyNamingPolicy = options.PropertyNamingPolicy,
            PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive,
            NumberHandling = options.NumberHandling,
            AllowTrailingCommas = options.AllowTrailingCommas
        };
    }
}