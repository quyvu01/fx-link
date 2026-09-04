using FxLink.Entities;
using FxLink.Outbox.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace FxLink.Outbox.EntityFrameworkCore.Extensions;

public static class ModelBuilderExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        public void AddOutboxMessageEntity()
        {
            modelBuilder.Entity<OutboxMessage>(builder =>
            {
                builder.HasKey(m => m.Id);
                builder.Property(m => m.Id).ValueGeneratedNever();
                builder.Property(m => m.Sequence).ValueGeneratedOnAdd();
                builder.HasIndex(m => new { m.PartitionKey, m.Sequence });
            });

            modelBuilder.Entity<OutboxPartitionLease>(builder =>
            {
                builder.HasKey(l => l.PartitionKey);
                builder.Property(l => l.PartitionKey).ValueGeneratedNever();
                builder.Property(l => l.Version).IsConcurrencyToken();
            });
        }
    }
}