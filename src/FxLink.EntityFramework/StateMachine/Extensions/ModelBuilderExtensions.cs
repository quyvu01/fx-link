using FxLink.StateMachine.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FxLink.StateMachine.EntityFrameworkCore.Extensions;

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
}