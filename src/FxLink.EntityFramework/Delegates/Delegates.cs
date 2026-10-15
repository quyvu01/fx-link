using Microsoft.EntityFrameworkCore;

namespace FxLink.EntityFramework.Delegates;

internal delegate DbContext GetDbContextByStateMachineInstance(Type stateMachineType);