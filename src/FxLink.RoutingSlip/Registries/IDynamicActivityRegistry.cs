namespace FxLink.RoutingSlip.Registries;

// Maps each dynamically-addressed activity's Type to the Uri it was registered under
// (AddActivity<TActivity>(uri)). RoutingSlipConsumer<DynamicRoutingMessage> reads this to check
// "does this incoming DynamicRoutingMessage.Destination actually belong to MY activityType" before
// doing anything else — every dynamically-addressed activity's queue is bound to the SAME
// DynamicRoutingMessage exchange, so this self-filter is what keeps them from stepping on each
// other, same role IExecuteActivity<,> reflection already plays for the ActivityLogEntry exchange.
internal interface IDynamicActivityRegistry
{
    string GetDestination(Type activityType);
}

internal sealed class DynamicActivityRegistry(IReadOnlyDictionary<Type, string> destinationsByActivityType)
    : IDynamicActivityRegistry
{
    public string GetDestination(Type activityType) =>
        destinationsByActivityType.GetValueOrDefault(activityType);
}
