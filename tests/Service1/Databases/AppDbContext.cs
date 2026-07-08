using Microsoft.EntityFrameworkCore;
using Service1.StateMachines.Inventory;
using Service1.StateMachines.Orders;

namespace Service1.Databases;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OrderStateMachineInstance> OrderStateMachines { get; set; }
    public DbSet<InventoryReservationInstance> InventoryReservations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Optimistic mode (OrderStateMachine): EF's own concurrency token, mapped to Postgres' xmin.
        modelBuilder.Entity<OrderStateMachineInstance>(e =>
        {
            e.HasKey(x => x.CorrelationId);
            e.Property(x => x.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .IsRowVersion();
        });

        // Pessimistic mode (InventoryReservationStateMachine): no concurrency token needed - the
        // advisory lock acquired in BeginScopeAsync is what serializes concurrent writers.
        modelBuilder.Entity<InventoryReservationInstance>(e => e.HasKey(x => x.CorrelationId));
    }
}
