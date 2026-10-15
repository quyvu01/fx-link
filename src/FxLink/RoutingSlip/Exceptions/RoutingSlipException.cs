using FxLink.Exceptions;

namespace FxLink.RoutingSlip.Exceptions;

/// <summary>
/// Groups the exceptions thrown by FxLink.RoutingSlip (activity execution/compensation reported as
/// faulted without an explicit exception).
/// </summary>
public static class RoutingSlipException
{
    /// <summary>An activity called IExecuteContext.Fault() without providing an exception.</summary>
    public sealed class ExecuteFaultedWithoutException(Type argumentType)
        : DistributedException(
            $"Execute for argument {argumentType.FullName} faulted without providing an exception. " +
            "Use Fault(exception) to include failure details.");

    /// <summary>An activity called ICompensateContext.Fault() without providing an exception.</summary>
    public sealed class CompensateFaultedWithoutException(Type logType)
        : DistributedException(
            $"Compensate for log {logType.FullName} faulted without providing an exception. " +
            "Use Fault(exception) to include failure details.");
}
