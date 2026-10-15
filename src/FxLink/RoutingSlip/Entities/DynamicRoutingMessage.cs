namespace FxLink.RoutingSlip.Entities;

// The single, statically-known wire type for every URI-addressed (dynamic) routing-slip step.
// Destination and Json are DATA, not this type's own identity — Type.GetType() on the receiver
// always resolves DynamicRoutingMessage itself (a real, shared type known at compile time on both
// sides), so FxLink.RabbitMq's core dispatch (props.Type / _connectorTypeCache) never needs to
// understand URIs at all. Which real activity a given message belongs to is resolved one level
// down, inside RoutingSlipConsumer, the same way ActivityLogEntry already lets several
// compensate-capable activities share one fanout exchange and self-filter by identity.
public sealed record DynamicRoutingMessage(string Destination, string Json);
