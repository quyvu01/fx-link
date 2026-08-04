using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Extensions;
using FxLink.Faults;
using FxLink.Serialization;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Configurators;
using FxLink.StateMachine.Contexts;
using FxLink.StateMachine.Delegates;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Registries;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Implementations.Workflows;

internal sealed class EventOperator<TInstance, TMessage>(IEvent<TMessage> @event, IStateMachine stateMachine)
    : IEventOperator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    public IEvent<TMessage> Event { get; } = @event;
    private readonly List<AsyncOperatorAction<TInstance, TMessage>> _asyncActions = [];

    public async Task ExecuteAsync(IStateMachineContext<TInstance, TMessage> context,
        CancellationToken token = default)
    {
        foreach (var asyncAction in _asyncActions) await asyncAction.Invoke(context, token);
    }

    public IEventOperator<TInstance, TMessage> Then([NotNull] OperatorAction<TInstance, TMessage> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ThenAsync(ActionAsAsync);

        Task ActionAsAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            action.Invoke(context);
            return Task.CompletedTask;
        }
    }

    public IEventOperator<TInstance, TMessage> ThenAsync([NotNull] AsyncOperatorAction<TInstance, TMessage> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        _asyncActions.Add(asyncAction);
        return this;
    }

    public IEventOperator<TInstance, TMessage> TransitionTo([NotNull] IState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return !stateMachine.States.Contains(state)
            ? throw new StateMachineException.StateNotDeclaredOnStateMachine(state.Name)
            : Then(StateTransitionAction);

        void StateTransitionAction(IStateMachineContext<TInstance, TMessage> context)
        {
            if (context.Instance is { } instance) instance.State = state.Name;
        }
    }

    public IEventOperator<TInstance, TMessage> Complete() => TransitionTo(stateMachine.Completed);

    public IEventOperator<TInstance, TMessage> If([NotNull] OperatorCondition<TInstance, TMessage> condition,
        [NotNull] ActivityOperatorCallback<TInstance, TMessage> callback) =>
        IfElse(condition, callback, x => x);

    public IEventOperator<TInstance, TMessage> IfAsync(
        [NotNull] AsyncOperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback) =>
        IfElseAsync(condition, callback, x => x);

    public IEventOperator<TInstance, TMessage> IfElse([NotNull] OperatorCondition<TInstance, TMessage> condition,
        [NotNull] ActivityOperatorCallback<TInstance, TMessage> succeedCallback,
        [NotNull] ActivityOperatorCallback<TInstance, TMessage> otherwiseCallback)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(succeedCallback);
        ArgumentNullException.ThrowIfNull(otherwiseCallback);

        return IfElseAsync(ConditionAsync, succeedCallback, otherwiseCallback);

        Task<bool> ConditionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = condition.Invoke(context);
            return Task.FromResult(conditionResult);
        }
    }

    public IEventOperator<TInstance, TMessage> IfElseAsync(
        [NotNull] AsyncOperatorCondition<TInstance, TMessage> condition,
        [NotNull] ActivityOperatorCallback<TInstance, TMessage> succeedCallback,
        [NotNull] ActivityOperatorCallback<TInstance, TMessage> otherwiseCallback)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(succeedCallback);
        ArgumentNullException.ThrowIfNull(otherwiseCallback);
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = await condition.Invoke(context, ct);
            var @operator = new EventOperator<TInstance, TMessage>(Event, stateMachine);
            var newOperator = conditionResult ? succeedCallback.Invoke(@operator) : otherwiseCallback.Invoke(@operator);
            await newOperator.ExecuteAsync(context, ct);
        }
    }

    public IEventOperator<TInstance, TMessage> Publish<T>(
        [NotNull] MessageOperatorFactory<TInstance, TMessage, T> messageFactory) where T : class
    {
        ArgumentNullException.ThrowIfNull(messageFactory);
        return PublishAsync(MessageFactoryAsync);

        Task<T> MessageFactoryAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            var message = messageFactory.Invoke(context);
            return Task.FromResult(message);
        }
    }

    public IEventOperator<TInstance, TMessage> Publish<T>(
        MessageOperatorFactory<TInstance, TMessage, object> messageFactory) where T : class =>
        Publish(ConvertMessageFactory<T>(messageFactory));

    public IEventOperator<TInstance, TMessage> PublishAsync<T>(
        [NotNull] MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class
    {
        ArgumentNullException.ThrowIfNull(messageFactoryAsync);
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var message = await messageFactoryAsync.Invoke(context, ct);
            var services = context.GetPayload<IServiceProvider>();
            var publisher = services.GetRequiredService<IPublisher>();
            await publisher.PublishAsync(message, ct);
        }
    }

    public IEventOperator<TInstance, TMessage> PublishAsync<T>(
        MessageOperatorFactoryAsync<TInstance, TMessage, object> messageFactoryAsync) where T : class =>
        PublishAsync(ConvertMessageAsyncFactory<T>(messageFactoryAsync));

    public IEventOperator<TInstance, TMessage> Response<T>(
        [NotNull] MessageOperatorFactory<TInstance, TMessage, T> messageFactory)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(messageFactory);
        return ResponseAsync(MessageFactoryAsync);

        Task<T> MessageFactoryAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            var message = messageFactory.Invoke(context);
            return Task.FromResult(message);
        }
    }

    public IEventOperator<TInstance, TMessage> Response<T>(
        MessageOperatorFactory<TInstance, TMessage, object> messageFactory) where T : class =>
        Response(ConvertMessageFactory<T>(messageFactory));

    public IEventOperator<TInstance, TMessage> ResponseAsync<T>(
        [NotNull] MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class
    {
        ArgumentNullException.ThrowIfNull(messageFactoryAsync);
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var message = await messageFactoryAsync.Invoke(context, ct);
            var consumerContext = new ConsumerContext<TMessage>
                (context.Message, context.RequesterId, context.CorrelationId, context.Headers);
            await consumerContext.ResponseAsync(message, ct);
        }
    }

    public IEventOperator<TInstance, TMessage> ResponseAsync<T>(
        MessageOperatorFactoryAsync<TInstance, TMessage, object> messageFactoryAsync) where T : class =>
        ResponseAsync(ConvertMessageAsyncFactory<T>(messageFactoryAsync));

    public IEventOperator<TInstance, TMessage> Schedule<T>([NotNull] ISchedule<T> schedule,
        [NotNull] MessageOperatorFactory<TInstance, TMessage, T> messageFactory) where T : class
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(messageFactory);
        return ScheduleAsync(schedule, MessageFactoryAsync);

        Task<T> MessageFactoryAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            var message = messageFactory.Invoke(context);
            return Task.FromResult(message);
        }
    }

    public IEventOperator<TInstance, TMessage> Schedule<T>(ISchedule<T> schedule,
        MessageOperatorFactory<TInstance, TMessage, object> messageFactory) where T : class =>
        Schedule(schedule, ConvertMessageFactory<T>(messageFactory));

    public IEventOperator<TInstance, TMessage> ScheduleAsync<T>([NotNull] ISchedule<T> schedule,
        [NotNull] MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(messageFactoryAsync);
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            if (!stateMachine.InternalActivityConfigurators.TryGetValue(schedule, out var configurator) ||
                configurator is not IScheduleConfigurator<TInstance, T> scheduleConfigurator) return;
            if (scheduleConfigurator is { Delay: not null, DelayProvider: not null })
                throw new StateMachineException.ScheduleDelayConfiguredTwice(schedule.Name);
            var message = await messageFactoryAsync.Invoke(context, ct);
            var delay = scheduleConfigurator.Delay ?? scheduleConfigurator.DelayProvider.Invoke(context);
            var services = context.GetPayload<IServiceProvider>();
            var publisher = services.GetRequiredService<IPublisher>();
            var tokenId = Id.New();
            var setter = scheduleConfigurator.TokenIdProvider.GetSetter();
            setter.Invoke(context.Instance, tokenId);
            var headers = new HeaderBag(context.Headers)
                .With(DistributedConfigurators.Headers.DeliveryKindKey, DistributedConfigurators.DeliveryKinds.Delay)
                .With(StateMachineConfigurators.MessageRoutingKey, schedule.Received.Name);
            var publisherContext = new PublisherContext(context.CorrelationId, headers)
            {
                DelayTime = delay,
                ScheduleToken = tokenId
            };
            publisher.SetContext(publisherContext);
            await publisher.PublishAsync(message, ct);
        }
    }

    public IEventOperator<TInstance, TMessage> ScheduleAsync<T>(ISchedule<T> schedule,
        MessageOperatorFactoryAsync<TInstance, TMessage, object> messageFactoryAsync) where T : class =>
        ScheduleAsync(schedule, ConvertMessageAsyncFactory<T>(messageFactoryAsync));

    public IEventOperator<TInstance, TMessage> Unschedule<T>([NotNull] ISchedule<T> schedule) where T : class
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return Then(ActionAsync);

        // We don't have the mechanism to cancel the event that published to message. So just need to remove the TokenId and handle it on gateway
        void ActionAsync(IStateMachineContext<TInstance, TMessage> context)
        {
            if (!stateMachine.InternalActivityConfigurators.TryGetValue(schedule, out var configurator) ||
                configurator is not IScheduleConfigurator<TInstance, T> scheduleConfigurator) return;
            var setter = scheduleConfigurator.TokenIdProvider.GetSetter();
            setter.Invoke(context.Instance, null); // Set the token Id to null
        }
    }

    public IEventOperator<TInstance, TMessage> Request<TRequest, TResponse>(IRequest<TRequest, TResponse> request,
        MessageOperatorFactory<TInstance, TMessage, TRequest> messageFactory)
        where TRequest : class where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(messageFactory);
        return RequestAsync(request, MessageFactoryAsync);

        Task<TRequest> MessageFactoryAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            var message = messageFactory.Invoke(context);
            return Task.FromResult(message);
        }
    }

    public IEventOperator<TInstance, TMessage> Request<TRequest, TResponse>(IRequest<TRequest, TResponse> request,
        MessageOperatorFactory<TInstance, TMessage, object> messageFactory)
        where TRequest : class where TResponse : class =>
        Request(request, ConvertMessageFactory<TRequest>(messageFactory));

    public IEventOperator<TInstance, TMessage> RequestAsync<TRequest, TResponse>(IRequest<TRequest, TResponse> request,
        MessageOperatorFactoryAsync<TInstance, TMessage, TRequest> messageFactoryAsync)
        where TRequest : class where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(messageFactoryAsync);
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            if (!stateMachine.InternalActivityConfigurators.TryGetValue(request, out var configurator) ||
                configurator is not IRequestConfigurator<TInstance, TRequest, TResponse> requestConfigurator) return;
            var message = await messageFactoryAsync.Invoke(context, ct);
            var serviceProvider = context.GetPayload<IServiceProvider>();
            // This architecture is different from request/reply patter
            // I will publish message instead of request message then wait it....
            var timeout = requestConfigurator.Timeout;
            var ttl = requestConfigurator.TimeToLive ?? timeout;
            var requestPublisher = serviceProvider.GetRequiredService<IPublisher>();

            var requestHeaders = new HeaderBag(context.Headers)
                .With(DistributedConfigurators.Headers.RequestSemanticsKey,
                    DistributedConfigurators.RequestSemantics.RequestAsPublisher)
                .With(StateMachineConfigurators.MessageRoutingKey, request.Completed.Name);
            var requestContext = new PublisherContext(context.CorrelationId, requestHeaders) { TimeToLive = ttl };
            requestPublisher.SetContext(requestContext);
            await requestPublisher.PublishAsync(message, ct);

            // Durable timeout watchdog: a delayed message that raises TimeoutExpired if it's still
            // relevant by the time it's delivered. Completed/Failed/TimeoutExpired are all bound only
            // During(request.Pending, ...), so whichever of the three arrives first transitions the
            // instance out of Pending; the other(s) then find no EventOperator for the new state and
            // are swallowed as StateMachineException.EventNotDeclaredForState — no extra token needed.
            var timeoutPublisher = serviceProvider.GetRequiredService<IPublisher>();
            var timeoutMessage = new RequestTimeoutExpired<TRequest>(message, context.CorrelationId,
                DateTime.UtcNow.Add(timeout), Id.New());
            var timeoutHeaders = new HeaderBag(context.Headers)
                .With(DistributedConfigurators.Headers.DeliveryKindKey, DistributedConfigurators.DeliveryKinds.Delay)
                .With(StateMachineConfigurators.MessageRoutingKey, request.TimeoutExpired.Name);
            var timeoutContext = new PublisherContext(context.CorrelationId, timeoutHeaders) { DelayTime = timeout };
            timeoutPublisher.SetContext(timeoutContext);
            await timeoutPublisher.PublishAsync(timeoutMessage, ct);
        }
    }

    public IEventOperator<TInstance, TMessage> RequestAsync<TRequest, TResponse>(IRequest<TRequest, TResponse> request,
        MessageOperatorFactoryAsync<TInstance, TMessage, object> messageFactoryAsync)
        where TRequest : class where TResponse : class =>
        RequestAsync(request, ConvertMessageAsyncFactory<TRequest>(messageFactoryAsync));

    public IEventOperator<TInstance, TMessage> Activity(StateMchineOperatorActivity<TInstance, TMessage> activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var stateMachineActivityConfigurator = new StateMachineActivityConfigurator<TInstance, TMessage>();
            activity.Invoke(stateMachineActivityConfigurator);
            stateMachineActivityConfigurator.ValidateItSelf();

            var services = context.GetPayload<IServiceProvider>();

            if (stateMachineActivityConfigurator.ActivityOfType is { } activityOfType)
            {
                var service = services
                    .GetRequiredKeyedService<IStateMachineActivity<TInstance, TMessage>>(activityOfType);
                var ctx = new StateMachineActivityContext<TInstance, TMessage>(context.Instance, context.Message,
                    context.RequesterId, context);
                try
                {
                    await service.ExecuteAsync(ctx, ct);
                }
                catch (Exception e)
                {
                    await service.FaultedAsync(ctx, e, ct);
                }

                await ExecuteTranslationInActivityAsync(ctx.TranslationToAction, context, ct);

                return;
            }

            if (stateMachineActivityConfigurator.ActivityOfInstanceType is { } activityOfInstanceType)
            {
                var service = services
                    .GetRequiredKeyedService<IStateMachineActivity<TInstance>>(activityOfInstanceType);
                var ctx = new StateMachineActivityContext<TInstance>(context.Instance, context.RequesterId, context);
                try
                {
                    await service.ExecuteAsync(ctx, ct);
                }
                catch (Exception e)
                {
                    await service.FaultedAsync(ctx, e, ct);
                }

                await ExecuteTranslationInActivityAsync(ctx.TranslationToAction, context, ct);
            }
        }
    }

    private async Task ExecuteTranslationInActivityAsync(Func<string> transitionAction,
        IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
    {
        if (transitionAction is null) return;
        var newOperator = new EventOperator<TInstance, TMessage>(Event, stateMachine);
        newOperator.TransitionTo(new State(transitionAction.Invoke()));
        await newOperator.ExecuteAsync(context, ct);
    }

    private static MessageOperatorFactory<TInstance, TMessage, T> ConvertMessageFactory<T>(
        MessageOperatorFactory<TInstance, TMessage, object> messageFactory) where T : class
    {
        ArgumentNullException.ThrowIfNull(messageFactory);
        return context =>
        {
            var values = messageFactory.Invoke(context);
            var message = MessageContractActivator.CreateFrom<T>(values);
            return message;
        };
    }

    private static MessageOperatorFactoryAsync<TInstance, TMessage, T> ConvertMessageAsyncFactory<T>(
        MessageOperatorFactoryAsync<TInstance, TMessage, object> messageFactoryAsync) where T : class
    {
        ArgumentNullException.ThrowIfNull(messageFactoryAsync);
        return async (context, ct) =>
        {
            var values = await messageFactoryAsync.Invoke(context, ct);
            var message = MessageContractActivator.CreateFrom<T>(values);
            return message;
        };
    }
}