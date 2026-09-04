using Microsoft.EntityFrameworkCore;

namespace FxLink.Outbox.EntityFrameworkCore.Wrappers;

internal record DbContextWrapped(DbContext DbContext);
