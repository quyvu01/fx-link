using FxLink.StateMachine.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using StateMachine.StateMachines.Inventory;

namespace StateMachine.Databases;

public sealed class StateMachineDbContext(DbContextOptions<StateMachineDbContext> options) : DbContext(options)
{
    public DbSet<InventoryReservationInstance> InventoryReservations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddStateMachineInstance<InventoryReservationInstance>();
        base.OnModelCreating(modelBuilder);
    }
}