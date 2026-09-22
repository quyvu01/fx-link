using FxLink.Messaging.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FxLink.Messaging.EntityFrameworkCore.Tests.TestSupport;

internal sealed class TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddOutboxMessageEntity();
}
