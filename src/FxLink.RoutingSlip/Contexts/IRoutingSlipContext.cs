using FxLink.Contexts;
using FxLink.RoutingSlip.Entities;

namespace FxLink.RoutingSlip.Contexts;

public interface IRoutingSlipContext : IPublisherContext
{
    IReadOnlyList<ItineraryStep> RemainingItineraries { get; }                                                                                                                                                      
    IReadOnlyList<ActivityLogEntry> ActivityLogs { get; }                                                                                                                                                          
    IHeaders Variables { get; } 
}