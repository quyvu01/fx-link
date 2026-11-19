using FxLink.Abstractions.StateMachine;

namespace FxLink.Implementations.StateMachines;

internal sealed record State(string Name) : IState;
