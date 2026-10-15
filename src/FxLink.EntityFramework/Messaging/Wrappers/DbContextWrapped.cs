using Microsoft.EntityFrameworkCore;

namespace FxLink.Messaging.EntityFrameworkCore.Wrappers;

internal record DbContextWrapped(DbContext DbContext);
