using FxLink.Outbox.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FxLink.Outbox.EntityFrameworkCore.Tests.TestSupport;

internal sealed class TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddOutboxMessageEntity();
}
