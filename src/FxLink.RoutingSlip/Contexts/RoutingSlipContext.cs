using FxLink.Contexts;
using FxLink.RoutingSlip.Configurators;
using FxLink.RoutingSlip.Entities;
using FxLink.RoutingSlip.Extensions;

namespace FxLink.RoutingSlip.Contexts;

internal sealed class RoutingSlipContext : PublisherContext, IRoutingSlipContext
{
    public RoutingSlipContext(Guid correlationId, IHeaders headers) : base(correlationId, headers)
    {
    }

    public RoutingSlipContext(IContext context) : base(context)
    {
    }

    // Backed by Headers, not plain auto-properties. ContextJsonConverter only preserves what the
    // SEND side attached to IContext (it serializes by runtime type) — the RECEIVE side always
    // reconstructs a plain FxLink-core ConsumerContext<TMessage> from a fixed DTO
    // (ConsumerContextSerializable), which has no idea IRoutingSlipContext exists and would silently
    // drop these on deserialize. Headers is the one channel that already round-trips end-to-end
    // without requiring FxLink core to know anything about this package.
    //
    // Get<T> is called with the property's own declared (interface) type, not a concrete List<T>:
    // HeaderBag.Get<T>'s same-process fast path does `is T typed`, which only succeeds if the stored
    // runtime value (e.g. an ItineraryStep[] built via `[.. args.Skip(1)]`) is assignable to T.
    // Arrays satisfy IReadOnlyList<T> but are NOT a List<T>, so typing this as List<T> would return
    // null for same-process reads (before any wire round-trip) despite having just been set.
    public IReadOnlyList<ItineraryStep> RemainingItineraries
    {
        get => Headers.Get<IReadOnlyList<ItineraryStep>>(RoutingSlipHeaders.RemainingItineraryKey);
        internal set => Headers.Set(RoutingSlipHeaders.RemainingItineraryKey, value);
    }

    public IReadOnlyList<ActivityLogEntry> ActivityLogs
    {
        get => Headers.Get<IReadOnlyList<ActivityLogEntry>>(RoutingSlipHeaders.ActivityLogKey);
        internal set => Headers.Set(RoutingSlipHeaders.ActivityLogKey, value);
    }

    // Flattened as x-variable-{key} entries on the outer Headers, not stored under one nested
    // IHeaders-typed value — see RoutingSlipHeaders.VariablePrefix for why nesting silently loses
    // data. The getter reconstructs the bag on demand; the setter fans each entry back out.
    public IHeaders Variables
    {
        get => Headers.GetVariables();
        internal set
        {
            if (value is null) return;
            foreach (var (key, variableValue) in value)
                Headers.Set($"{RoutingSlipHeaders.VariablePrefix}{key}", variableValue);
        }
    }
}
