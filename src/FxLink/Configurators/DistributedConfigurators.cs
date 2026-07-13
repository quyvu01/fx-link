using System.Text.Json;

namespace FxLink.Configurators;

public static class DistributedConfigurators
{
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static readonly TimeSpan[] DefaultRetryPolicy =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];
}