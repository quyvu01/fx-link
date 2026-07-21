namespace StateMachine.Dtos.Inventory;

// Bound via DuringAny(...) on InventoryReservationStateMachine, so it can be asked in any known state.
public sealed class GetReservationSummary
{
    public Guid OrderId { get; set; }
}

public sealed class ReservationSummaryResponse
{
    public Guid OrderId { get; set; }
    public string State { get; set; }
    public DateTime SummarizedAt { get; set; }
}
