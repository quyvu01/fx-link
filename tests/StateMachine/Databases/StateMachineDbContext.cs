using Microsoft.EntityFrameworkCore;
using StateMachine.StateMachines.Inventory;

namespace StateMachine.Databases;

public sealed class StateMachineDbContext(DbContextOptions<StateMachineDbContext> options) : DbContext(options)
{
    public DbSet<InventoryReservationInstance> InventoryReservations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pessimistic mode (InventoryReservationStateMachine): no concurrency token needed - the
        // advisory lock acquired in BeginScopeAsync is what serializes concurrent writers.
        modelBuilder.Entity<InventoryReservationInstance>(e => e.HasKey(x => x.CorrelationId));
    }
}
