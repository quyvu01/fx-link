namespace FxLink.RoutingSlip.Abstractions;

public interface IExecuteActivity<TArguments> where TArguments : class
{
}

public interface IExecuteActivity<TArguments, TLogs> : IExecuteActivity<TArguments>
    where TArguments : class where TLogs : class
{
}