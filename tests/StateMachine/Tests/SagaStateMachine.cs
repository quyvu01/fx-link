using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations.StateMachines;
using FxLink.Statics;
using StateMachine.Tests.Events;

namespace StateMachine.Tests;

public sealed class SagaStateMachine : StateMachine<SagaStateMachineInstance>
{
    public IEvent<IInitTest> TestInitialized { get; private set; }
    public IRequest<IGetName, INameResponse> GetName { get; private set; }

    public SagaStateMachine(ILogger<SagaStateMachine> logger)
    {
        Event(TestInitialized, x => x.CorrelationBy((_, _) => false).SelectId(_ => Id.New()));
        Request(GetName, x =>
        {
            x.Timeout = TimeSpan.FromSeconds(5);
            x.TimeToLive = TimeSpan.FromSeconds(7);
            x.Completed = c => c.CorrelationBy((isn, ct) => isn.Name == ct.Message.Name);
            x.Failed = c => c.CorrelationBy((isn, ct) => isn.Name == ct.Message.Message.Name);
            x.TimeoutExpired = c => c.CorrelationBy((isn, ct) => isn.Name == ct.Message.Message.Name);
        });

        Initially(When(TestInitialized)
            .Then(ctx =>
            {
                ctx.Instance.Name = ctx.Message.Name;
                logger.LogInformation("[TestInitialized] message: {@Message}", ctx.Message);
            })
            .Request(GetName, ctx => ctx.Instance)
            .TransitionTo(GetName.Pending));

        During(GetName.Pending, When(GetName.Completed)
                .Then(ctx => logger.LogInformation("[GetName.Completed] message: {@Message}", ctx.Message)),
            When(GetName.TimeoutExpired)
                .Then(ctx => logger.LogInformation("[GetName.TimeoutExpired] message: {@Message}", ctx.Message)),
            When(GetName.Failed)
                .Then(ctx => logger.LogInformation("[GetName.Failed] message: {@Message}", ctx.Message))
        );
    }
}