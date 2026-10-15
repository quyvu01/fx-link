using FxLink.Entities;
using FxLink.EntityFramework.Inbox.Entities;
using FxLink.EntityFramework.Outbox.Entities;
using FxLink.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FxLink.EntityFramework.Extensions;

public static class ModelBuilderExtensions
{
    public static void StateMachineInstanceVersion<TInstance>(this EntityTypeBuilder<TInstance> entityTypeBuilder)
        where TInstance : class, IStateMachineInstance, IVersion =>
        entityTypeBuilder.Property(x => x.Version).IsConcurrencyToken();

    public static EntityTypeBuilder<TInstance> AddStateMachineInstance<TInstance>(this ModelBuilder modelBuilder)
        where TInstance : class, IStateMachineInstance
    {
        var entityBuilder = modelBuilder.Entity<TInstance>();
        entityBuilder.HasKey(x => x.CorrelationId);
        return entityBuilder;
    }

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

        public void AddInboxRecordEntity()
        {
            modelBuilder.Entity<InboxRecord>(builder =>
            {
                builder.HasKey(r => new { r.ConsumerKey, r.MessageId });
                builder.Property(r => r.Version).IsConcurrencyToken();
            });
        }
    }
}
