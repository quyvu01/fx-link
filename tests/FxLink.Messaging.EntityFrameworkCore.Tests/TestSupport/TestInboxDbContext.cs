using FxLink.EntityFramework.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FxLink.EntityFramework.Tests.TestSupport;

internal sealed class TestInboxDbContext(DbContextOptions<TestInboxDbContext> options) : DbContext(options)
{
    // Stands in for the consumer's own business data, which shares this DbContext with the Inbox store.
    public DbSet<TestNote> Notes => Set<TestNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddInboxRecordEntity();
}

internal sealed class TestNote
{
    public Guid Id { get; set; }
    public string Text { get; set; }
}
