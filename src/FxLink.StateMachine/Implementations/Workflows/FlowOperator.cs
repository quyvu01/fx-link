using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Delegates;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Implementations.Workflows;

public sealed class FlowOperator<TInstance, TMessage>(IEvent<TMessage> @event) : IFlowOperator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    public IEvent<TMessage> Event { get; } = @event;
    private readonly List<AsyncOperatorAction<TInstance, TMessage>> _asyncActions = [];
    public AsyncOperatorAction<TInstance, TMessage>[] AsyncActions => [.._asyncActions];

    public IFlowOperator<TInstance, TMessage> Then(OperatorAction<TInstance, TMessage> action)
    {
        _asyncActions.Add(ActionAsAsync);
        return this;

        Task ActionAsAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            action.Invoke(context);
            return Task.CompletedTask;
        }
    }

    public IFlowOperator<TInstance, TMessage> ThenAsync(AsyncOperatorAction<TInstance, TMessage> asyncAction)
    {
        _asyncActions.Add(asyncAction);
        return this;
    }

    public IFlowOperator<TInstance, TMessage> TransitionTo(IState state)
    {
        Then(StateTransitionAction);
        return this;

        void StateTransitionAction(IStateMachineContext<TInstance, TMessage> context)
        {
            if (context.Instance is { } instance) instance.State = state.Name;
        }
    }

    public IFlowOperator<TInstance, TMessage> If(OperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback)
    {
        return IfAsync(ConditionAsync, callback);

        Task<bool> ConditionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = condition.Invoke(context);
            return Task.FromResult(conditionResult);
        }
    }

    public IFlowOperator<TInstance, TMessage> IfAsync(
        AsyncOperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback)
    {
        ThenAsync(ConditionActionAsync);
        return this;

        async Task ConditionActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = await condition.Invoke(context, ct);
            if (!conditionResult) return;
            var newFlow = callback.Invoke(new FlowOperator<TInstance, TMessage>(Event));
            foreach (var asyncAction in newFlow.AsyncActions) await asyncAction.Invoke(context, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> IfElse(OperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback,
        ActivityOperatorCallback<TInstance, TMessage> otherwiseCallback)
    {
        return IfElseAsync(ConditionAsync, callback, otherwiseCallback);

        Task<bool> ConditionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = condition.Invoke(context);
            return Task.FromResult(conditionResult);
        }
    }

    public IFlowOperator<TInstance, TMessage> IfElseAsync(
        AsyncOperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback,
        ActivityOperatorCallback<TInstance, TMessage> otherwiseCallback)
    {
        ThenAsync(ConditionActionAsync);
        return this;

        async Task ConditionActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = await condition.Invoke(context, ct);
            var @operator = new FlowOperator<TInstance, TMessage>(Event);
            var newFlow = conditionResult ? callback.Invoke(@operator) : otherwiseCallback.Invoke(@operator);
            foreach (var asyncAction in newFlow.AsyncActions) await asyncAction.Invoke(context, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> Publish<T>(
        MessageOperatorFactory<TInstance, TMessage, T> messageFactory) where T : class
    {
        return PublishAsync(MessageFactoryAsync);

        Task<T> MessageFactoryAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            var message = messageFactory.Invoke(context);
            return Task.FromResult(message);
        }
    }

    public IFlowOperator<TInstance, TMessage> PublishAsync<T>(
        MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class
    {
        ThenAsync(PublishActionAsync);
        return this;

        async Task PublishActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var message = await messageFactoryAsync.Invoke(context, ct);
            var services = ServiceProviderAmbient.Services;
            var publisher = services.GetRequiredService<IPublisher>();
            await publisher.PublishAsync(message, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> Response<T>(MessageOperatorFactory<TInstance, TMessage, T> messageFactory)
        where T : class
    {
        return ResponseAsync(MessageFactoryAsync);

        Task<T> MessageFactoryAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            var message = messageFactory.Invoke(context);
            return Task.FromResult(message);
        }
    }

    public IFlowOperator<TInstance, TMessage> ResponseAsync<T>(
        MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class
    {
        return ThenAsync(ConditionActionAsync);

        async Task ConditionActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var message = await messageFactoryAsync.Invoke(context, ct);
            var services = ServiceProviderAmbient.Services;
            var client = services.GetService<IClient<T>>();
            if (client is null) return;
            var correlationId = context.CorrelationId;
            var headers = context.Headers;
            await client.SendAsync(message, new ResponseContext(correlationId, headers), ct);
        }
    }
}