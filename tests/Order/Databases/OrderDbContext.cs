using FxLink.Outbox.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Order.Databases;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutboxMessageEntity();
        base.OnModelCreating(modelBuilder);
    }
}