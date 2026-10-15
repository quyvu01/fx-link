using Microsoft.EntityFrameworkCore;

namespace FxLink.StateMachine.EntityFrameworkCore.Delegates;

internal delegate DbContext GetDbContextByStateMachineInstance(Type stateMachineType);