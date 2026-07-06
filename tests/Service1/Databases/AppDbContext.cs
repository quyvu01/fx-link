using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Service1.StateMachines;

namespace Service1.Databases;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OrderStateMachineInstance> OrderStateMachines { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var converter = new ValueConverter<byte[], uint>(
            v => BitConverter.ToUInt32(v, 0),
            v => BitConverter.GetBytes(v)
        );
        modelBuilder.Entity<OrderStateMachineInstance>(e =>
        {
            e.HasKey(x => x.CorrelationId);
            e.Property(x => x.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .IsRowVersion();
        });
    }
}