using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Delegates;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Registries;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Implementations.Workflows;

public sealed class FlowOperator<TInstance, TMessage>(IEvent<TMessage> @event, IStateMachine stateMachine)
    : IFlowOperator<TInstance, TMessage>
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
        ThenAsync(ActionAsync);
        return this;

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = await condition.Invoke(context, ct);
            if (!conditionResult) return;
            var newFlow = callback.Invoke(new FlowOperator<TInstance, TMessage>(Event, stateMachine));
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
        ThenAsync(ActionAsync);
        return this;

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = await condition.Invoke(context, ct);
            var @operator = new FlowOperator<TInstance, TMessage>(Event, stateMachine);
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
        ThenAsync(ActionAsync);
        return this;

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
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
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var message = await messageFactoryAsync.Invoke(context, ct);
            var consumerContext = new ConsumerContext<TMessage>
                (context.Message, context.CorrelationId, context.RequesterId, context.Headers);
            await consumerContext.ResponseAsync(message, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> Schedule<T>(ISchedule<T> schedule,
        MessageOperatorFactory<TInstance, TMessage, T> messageFactory) where T : class
    {
        return ScheduleAsync(schedule, MessageFactoryAsync);

        Task<T> MessageFactoryAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            var message = messageFactory.Invoke(context);
            return Task.FromResult(message);
        }
    }

    public IFlowOperator<TInstance, TMessage> ScheduleAsync<T>(ISchedule<T> schedule,
        MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactory) where T : class
    {
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var message = await messageFactory.Invoke(context, ct);
            if (!stateMachine.ActivityConfigurators.TryGetValue(schedule, out var configurator) ||
                configurator is not IScheduleConfigurator<TInstance, T> scheduleConfigurator) return;
            if (scheduleConfigurator is { Delay: not null, DelayProvider: not null })
                throw new StateMachineException.ScheduleTimeCannotBeRegisteredBothDelayAndDelayProvider(schedule.Name);
            var delay = scheduleConfigurator.Delay ?? scheduleConfigurator.DelayProvider.Invoke(context);
            var publisher = ServiceProviderAmbient.Services.GetRequiredService<IPublisher>();
            var tokenId = Guid.NewGuid();
            var setter = scheduleConfigurator.TokenIdProvider.GetSetter();
            setter.Invoke(context.Instance, tokenId);
            await publisher.PublishAsync(message, new PublisherContext(context.CorrelationId, context.Headers)
                { Delay = delay, ScheduledMessageId = tokenId }, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> Unschedule<T>(ISchedule<T> schedule) where T : class
    {
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            if (!stateMachine.ActivityConfigurators.TryGetValue(schedule, out var configurator) ||
                configurator is not IScheduleConfigurator<TInstance, T> scheduleConfigurator) return;
            var publisher = ServiceProviderAmbient.Services.GetRequiredService<IPublisher>();
            var setter = scheduleConfigurator.TokenIdProvider.GetSetter();
            var tokenId = scheduleConfigurator.TokenIdProvider.Compile().Invoke(context.Instance);
            await publisher.PublishAsync(new DiscardMessagePublished<T>(tokenId),
                new PublisherContext(context.CorrelationId, context.Headers), ct);
            setter.Invoke(context.Instance, null); // Set the token Id to null
        }
    }
}