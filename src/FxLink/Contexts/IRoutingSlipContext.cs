using FxLink.Contexts;
using FxLink.Entities;

namespace FxLink.Contexts;

public interface IRoutingSlipContext : IPublishContext
{
    IReadOnlyList<ItineraryStep> RemainingItineraries { get; }                                                                                                                                                      
    IReadOnlyList<ActivityLogEntry> ActivityLogs { get; }                                                                                                                                                          
    IHeaders Variables { get; } 
}
