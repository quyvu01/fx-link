using Microsoft.EntityFrameworkCore;

namespace FxLink.StateMachine.EntityFrameworkCore.Wrappers;

internal record DbContextWrapped(DbContext DbContext);