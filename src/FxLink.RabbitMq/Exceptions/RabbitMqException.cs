using FxLink.Exceptions;

namespace FxLink.RabbitMq.Exceptions;

/// <summary>
/// Groups the exceptions thrown by FxLink.RabbitMq.
/// </summary>
public static class RabbitMqException
{
    /// <summary>
    /// Two or more locally-consumed message types resolved to the same exchange name — either two
    /// custom IMessageConfigurator.Name() calls collided, or a custom name collided with another
    /// type's auto-generated name. RabbitMQ would fan out both types' messages to every queue bound
    /// to that exchange, so this is caught at startup rather than left to misroute silently.
    /// </summary>
    public sealed class DuplicateExchangeName(string exchangeName, IReadOnlyCollection<Type> messageTypes) :
        DistributedException(
            $"Exchange name '{exchangeName}' is used by multiple message types with local consumers: " +
            $"{string.Join(", ", messageTypes.Select(t => t.FullName))}. Give each message type a distinct " +
            $"name via IMessageConfigurator<TMessage>.Name(...) (or rename the colliding type).");
}
