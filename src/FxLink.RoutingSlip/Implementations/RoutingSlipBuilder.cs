using System.Text.Json;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Entities;

namespace FxLink.RoutingSlip.Implementations;

internal sealed class RoutingSlipBuilder : IRoutingSlipBuilder
{
    internal IReadOnlyCollection<ItineraryStep> ItinerarySteps => _itinerarySteps;
    internal IHeaders Headers => new HeaderBag(_headerBags);
    private readonly List<ItineraryStep> _itinerarySteps = [];
    private readonly Dictionary<string, object> _headerBags = [];

    public IRoutingSlipBuilder AddArgument<TArgument>(TArgument arguments)
    {
        var json = JsonSerializer.Serialize(arguments, DistributedConfigurators.JsonSerializerOptions);
        _itinerarySteps.Add(new ItineraryStep(typeof(TArgument).AssemblyQualifiedName, json));
        return this;
    }

    public IRoutingSlipBuilder SetVariable(string key, object value)
    {
        _headerBags[key] = value;
        return this;
    }
}