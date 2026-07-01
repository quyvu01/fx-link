using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Exceptions;

public static class StateMachineException
{
    public sealed class StateConfigurationIsNotCorrect(Type type) :
        Exception(
            $"{type.FullName} is not configured correctly. Try to declare like `public {nameof(IState)} {type.Name} {{get; private set;}}`");

    public sealed class EventIsNotCorrect(Type type) :
        Exception(
            $"{type.FullName} is not configured correctly. Try to declare like `public {nameof(IActivity)}<{type.Name}> {type.Name}Event {{get; private set;}}`");

    public sealed class ActivityHasBeenConfiguration(Type activityType)
        : Exception($"Activity: {activityType.FullName} has been configuration. Do not config it twice");
    
    public sealed class MessageTypeHasBeenConfiguration(Type messageType)
        : Exception($"Message type: {messageType.FullName} cannot be duplicated on StateMachine");

    public sealed class EventDoesNotMatchAnyFlow(Type eventType)
        : Exception($"{eventType.FullName} did not match any flow!");

    public sealed class StateMachineInstanceMustBeInitFirst()
        : Exception("State machine instance need to be initialized first!");

    public sealed class CorrelationMethodMustBeInvoked()
        : Exception("No correlation configured. Call CorrelationId or CorrelationBy -> SelectId first.");

    public sealed class EventWasNotDeclaredForInstanceState(Type eventType, string state) :
        Exception($"Event: {eventType.FullName} was not declared for state: {state}");

    public sealed class ScheduleTimeCannotBeRegisteredBothDelayAndDelayProvider(string scheduleName)
        : Exception($"Schedule: {scheduleName} time cannot be configured for both Delay and DelayProvider");
    
    public sealed class ScheduleTimeMustBeRegister(string scheduleName)
        : Exception($"Schedule: {scheduleName} time must be registered for Delay or DelayProvider");
}