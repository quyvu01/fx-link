using Microsoft.EntityFrameworkCore;

namespace FxLink.EntityFramework.Wrappers;

internal record DbContextWrapped(DbContext DbContext);
