using FxLink.Exceptions;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Exceptions;

/// <summary>
/// Groups the exceptions thrown by FxLink.StateMachine (state/event declaration, flow configuration,
/// instance lifecycle).
/// </summary>
public static class StateMachineException
{
    /// <summary>A state property was not declared as `public IState X { get; private set; }`.</summary>
    public sealed class StateDeclarationIsInvalid(Type type) :
        DistributedException(
            $"{type.FullName} is not configured correctly. Try to declare like `public {nameof(IState)} {type.Name} {{get; private set;}}`");

    /// <summary>An event/activity property was not declared as `public IActivity&lt;T&gt; XEvent { get; private set; }`.</summary>
    public sealed class EventDeclarationIsInvalid(Type type) :
        DistributedException(
            $"{type.FullName} is not configured correctly. Try to declare like `public {nameof(IActivity)}<{type.Name}> {type.Name}Event {{get; private set;}}`");

    /// <summary>Event(...)/Request(...)/Schedule(...) was called more than once for the same activity type.</summary>
    public sealed class ActivityAlreadyConfigured(Type activityType)
        : DistributedException($"Activity: {activityType.FullName} has been configured. Do not configure it twice");

    /// <summary>The same message type was registered on more than one event within the same state machine.</summary>
    public sealed class MessageTypeAlreadyConfigured(Type messageType)
        : DistributedException($"Message type: {messageType.FullName} cannot be duplicated on StateMachine");

    /// <summary>An incoming event does not match any flow declared via Initially(...)/During(...).</summary>
    public sealed class NoFlowMatchesEvent(Type eventType)
        : DistributedException($"{eventType.FullName} did not match any flow!");

    /// <summary>
    /// An event arrived for a correlation id with no existing instance, and no OnMissingInstance(...)
    /// behavior was configured to handle it.
    /// </summary>
    public sealed class InstanceMustBeInitializedFirst()
        : DistributedException("State machine instance need to be initialized first!");

    /// <summary>Neither CorrelationId(...) nor CorrelationBy(...) + SelectId(...) was configured for this event.</summary>
    public sealed class CorrelationNotConfigured()
        : DistributedException("No correlation configured. Call CorrelationId or CorrelationBy -> SelectId first.");

    /// <summary>The event was raised while the instance is in a state that never declared handling for it.</summary>
    public sealed class EventNotDeclaredForState(Type eventType, string state) :
        DistributedException($"Event: {eventType.FullName} was not declared for state: {state}");

    /// <summary>A Schedule(...) configured both Delay(...) and DelayProvider(...), which are mutually exclusive.</summary>
    public sealed class ScheduleDelayConfiguredTwice(string scheduleName)
        : DistributedException($"Schedule: {scheduleName} time cannot be configured for both Delay and DelayProvider");

    /// <summary>A Schedule(...) configured neither Delay(...) nor DelayProvider(...).</summary>
    public sealed class ScheduleDelayNotConfigured(string scheduleName)
        : DistributedException($"Schedule: {scheduleName} time must be registered for Delay or DelayProvider");

    /// <summary>
    /// The message was intentionally faulted because OnMissingInstance(cfg => cfg.Fault()) was configured
    /// and no matching instance was found for the incoming event.
    /// </summary>
    public sealed class MissingInstanceFaulted()
        : DistributedException(
            "The message was explicitly faulted because no matching state machine instance was found.");

    /// <summary>An Activity(...) configured both OfType(...) and OfInstanceType(...), which are mutually exclusive.</summary>
    public sealed class ActivityTypeConfiguredTwice(Type instanceType, Type messageType) :
        DistributedException(
            $"Activity configuration for instance {instanceType.FullName} / message {messageType.FullName} cannot call both OfType(...) and OfInstanceType(...). Choose one.");

    /// <summary>An Activity(...) configured neither OfType(...) nor OfInstanceType(...).</summary>
    public sealed class ActivityTypeNotConfigured(Type instanceType, Type messageType) :
        DistributedException(
            $"Activity configuration for instance {instanceType.FullName} / message {messageType.FullName} must call either OfType(...) or OfInstanceType(...).");

    /// <summary>TransitionTo(...) was called with a state that was not declared on the state machine.</summary>
    public sealed class StateNotDeclaredOnStateMachine(string stateName) :
        DistributedException($"State: {stateName} is not declared on this state machine. Declare it as a `public {nameof(IState)}` property first.");

    /// <summary>
    /// A state machine instance type must declare exactly one public constructor, and it must be
    /// parameterless, so InstanceUltilities can create instances without guessing constructor arguments.
    /// </summary>
    public sealed class InstanceMustHaveParameterlessConstructor(Type instanceType) :
        DistributedException(
            $"{instanceType.FullName} must declare exactly one public constructor, and it must have no parameters.");
}