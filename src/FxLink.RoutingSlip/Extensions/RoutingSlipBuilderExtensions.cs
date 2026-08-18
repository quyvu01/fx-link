using System.Text.Json;
using FxLink.Configurators;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Entities;

namespace FxLink.RoutingSlip.Extensions;

public static class RoutingSlipBuilderExtensions
{
    // No fake/emitted type needed: DynamicRoutingMessage is a real, shared type known at compile
    // time on both ends, so this is just an ordinary AddArgument<TArguments> call with
    // TArguments = DynamicRoutingMessage. The actual destination and payload travel as DATA inside
    // it — see DynamicRoutingMessage's own comment for why that matters on the receiving side.
    public static IRoutingSlipBuilder AddArgument(this IRoutingSlipBuilder builder, Uri uri, object arguments)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(arguments);
        var json = JsonSerializer.Serialize(arguments, DistributedConfigurators.JsonSerializerOptions);
        return builder.AddArgument(new DynamicRoutingMessage(uri.ToString(), json));
    }
}