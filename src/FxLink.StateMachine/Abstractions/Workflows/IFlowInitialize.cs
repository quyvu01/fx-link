namespace FxLink.StateMachine.Abstractions.Workflows;

public interface IFlowInitialize : IFlow
{
    void Initially(params IFlow[] flows);
    void During(IState state, params IFlow[] flows);
}