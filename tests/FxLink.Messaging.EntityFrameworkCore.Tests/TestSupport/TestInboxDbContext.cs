using FxLink.Messaging.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FxLink.Messaging.EntityFrameworkCore.Tests.TestSupport;

internal sealed class TestInboxDbContext(DbContextOptions<TestInboxDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddInboxRecordEntity();
}
