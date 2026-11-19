using FxLink.Abstractions;
using FxLink.Registries;
using FxLink.Registries.Implementations;

namespace FxLink.Extensions;

internal static class ConfiguratorExtensions
{
    internal static IMessageKeys MessageKeys(this IConfigurator configurator) =>
        ((Configurator)configurator)?.MessageKeys ?? throw new Exception("Should be Configurator!");
}