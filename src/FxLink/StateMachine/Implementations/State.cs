using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Implementations;

internal sealed record State(string Name) : IState;