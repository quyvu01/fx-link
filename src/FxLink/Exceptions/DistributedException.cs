namespace FxLink.Exceptions;

/// <summary>
/// Common base type for every exception thrown by the FxLink libraries (FxLink, FxLink.StateMachine,
/// FxLink.StateMachine.EntityFrameworkCore, ...). Lets consumers catch one type to distinguish
/// FxLink-originated failures (misconfiguration, invalid state, concurrency conflicts, ...) from
/// arbitrary BCL or third-party exceptions.
/// </summary>
public abstract class DistributedException : Exception
{
    protected DistributedException(string message) : base(message)
    {
    }

    protected DistributedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
