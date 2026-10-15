using FxLink.EntityFramework.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FxLink.EntityFramework.Tests.TestSupport;

internal sealed class TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddOutboxMessageEntity();
}
