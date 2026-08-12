namespace FxLink.RoutingSlip.Configurators;

// Reserved header keys owned exclusively by FxLink.RoutingSlip — nobody else reads or writes them,
// so there's no risk of the multi-owner key collisions that motivated moving delivery/retry state
// out of headers in FxLink core. RoutingSlipContext is the only writer; RoutingSlipConsumer the
// only reader. Kept internal: external code goes through IRoutingSlipContext/IExecuteContext, never
// these raw keys directly.
internal static class RoutingSlipHeaders
{
    internal const string RemainingItineraryKey = "x-routingslip-remaining-itinerary";
    internal const string ActivityLogKey = "x-routingslip-activity-log";

    // Each SetVariable(key, value) lands under its own "x-variable-{key}" entry rather than one
    // nested IHeaders-typed value. HeadersJsonConverter.Write serializes each header entry by its
    // RUNTIME type (headerValue.GetType()) — a live HeaderBag's runtime type is the concrete
    // HeaderBag class, not IHeaders, so the converter (which only matches the exact IHeaders type)
    // never fires for it; the nested bag then silently serializes as an empty object via default
    // reflection, since HeaderBag exposes no public properties. Flattening under a prefix reuses the
    // exact same primitive-value round-trip already proven for every other header in the codebase.
    internal const string VariablePrefix = "x-variable-";
}
