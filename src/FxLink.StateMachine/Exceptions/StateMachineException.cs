using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Exceptions;

public static class StateMachineException
{
    public sealed class StateConfigurationIsNotCorrect(Type type) :
        Exception(
            $"{type.FullName} is not configured correctly. Try to declare like `public {nameof(IState)} {type.Name} {{get; private set;}}`");

    public sealed class EventIsNotCorrect(Type type) :
        Exception(
            $"{type.FullName} is not configured correctly. Try to declare like `public {nameof(IEvent)}<{type.Name}> {type.Name}Event {{get; private set;}}`");

    public sealed class EventHasBeenConfiguration(Type eventType)
        : Exception($"Event: {eventType.FullName} has been configuration. Do not config it twice");

    public sealed class EventDoesNotMatchAnyFlow(Type eventType)
        : Exception($"{eventType.FullName} did not match any flow!");

    public sealed class StateMachineInstanceMustBeInitFirst()
        : Exception("State machine instance need to be initialized first!");

    public sealed class CorrelationMethodMustBeInvoked()
        : Exception("No correlation configured. Call CorrelationId or CorrelationBy -> SelectId first.");

    public sealed class EventWasNotDeclaredForInstanceState(Type eventType, string state) :
        Exception($"Event: {eventType.FullName} was not declared for state: {state}")
    {
        public Type ResponseType { get; set; }
    }
}