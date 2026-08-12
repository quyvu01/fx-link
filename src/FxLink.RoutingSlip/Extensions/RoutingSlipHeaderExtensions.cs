using FxLink.Contexts;
using FxLink.RoutingSlip.Configurators;

namespace FxLink.RoutingSlip.Extensions;

internal static class RoutingSlipHeaderExtensions
{
    // Reconstructs the flattened x-variable-* entries into a plain IHeaders bag, stripping the
    // prefix — mirrors what RoutingSlipContext.Variables' setter wrote on the send side.
    internal static IHeaders GetVariables(this IHeaders headers)
    {
        var variables = new HeaderBag();
        foreach (var (key, value) in headers)
        {
            if (!key.StartsWith(RoutingSlipHeaders.VariablePrefix, StringComparison.OrdinalIgnoreCase)) continue;
            variables.Set(key[RoutingSlipHeaders.VariablePrefix.Length..], value);
        }

        return variables;
    }
}
