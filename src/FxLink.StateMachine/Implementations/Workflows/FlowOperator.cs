using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Entities;
using FxLink.Extensions;
using FxLink.Faults;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Contexts;
using FxLink.StateMachine.Delegates;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Registries;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Implementations.Workflows;

internal sealed class FlowOperator<TInstance, TMessage>(IEvent<TMessage> @event, IStateMachine stateMachine)
    : IFlowOperator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    public IEvent<TMessage> Event { get; } = @event;
    private readonly List<AsyncOperatorAction<TInstance, TMessage>> _asyncActions = [];
    public AsyncOperatorAction<TInstance, TMessage>[] AsyncActions => [.._asyncActions];

    public IFlowOperator<TInstance, TMessage> Then([NotNull] OperatorAction<TInstance, TMessage> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ThenAsync(ActionAsAsync);

        Task ActionAsAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken _)
        {
            action.Invoke(context);
            return Task.CompletedTask;
        }
    }

    public IFlowOperator<TInstance, TMessage> ThenAsync([NotNull] AsyncOperatorAction<TInstance, TMessage> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        _asyncActions.Add(asyncAction);
        return this;
    }

    public IFlowOperator<TInstance, TMessage> TransitionTo([NotNull] IState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Then(StateTransitionAction);
        return this;

        void StateTransitionAction(IStateMachineContext<TInstance, TMessage> context)
        {
            if (context.Instance is { } instance) instance.State = state.Name;
        }
    }

    public IFlowOperator<TInstance, TMessage> Complete() => TransitionTo(stateMachine.Completed);

    public IFlowOperator<TInstance, TMessage> If([NotNull] OperatorCondition<TInstance, TMessage> condition,
        [NotNull] ActivityOperatorCallback<TInstance, TMessage> callback) =>
        IfElse(condition, callback, x => x);

    public IFlowOperator<TInstance, TMessage> IfAsync(
        [NotNull] AsyncOperatorCondition<TInstance, TMessage> condition,
        ActivityOperatorCallback<TInstance, TMessage> callback) =>
        IfElseAsync(condition, callback, x => x);

    public IFlowOperator<TInstance, TMessage> IfElse([NotNull] OperatorCondition<TInstance, TMessage> condition,
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

    public IFlowOperator<TInstance, TMessage> IfElseAsync(
        [NotNull] AsyncOperatorCondition<TInstance, TMessage> condition,
        [NotNull] ActivityOperatorCallback<TInstance, TMessage> succeedCallback,
        [NotNull] ActivityOperatorCallback<TInstance, TMessage> otherwiseCallback)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(succeedCallback);
        ArgumentNullException.ThrowIfNull(otherwiseCallback);
        ThenAsync(ActionAsync);
        return this;

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var conditionResult = await condition.Invoke(context, ct);
            var @operator = new FlowOperator<TInstance, TMessage>(Event, stateMachine);
            var newFlow = conditionResult ? succeedCallback.Invoke(@operator) : otherwiseCallback.Invoke(@operator);
            foreach (var asyncAction in newFlow.AsyncActions) await asyncAction.Invoke(context, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> Publish<T>(
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

    public IFlowOperator<TInstance, TMessage> PublishAsync<T>(
        [NotNull] MessageOperatorFactoryAsync<TInstance, TMessage, T> messageFactoryAsync) where T : class
    {
        ArgumentNullException.ThrowIfNull(messageFactoryAsync);
        ThenAsync(ActionAsync);
        return this;

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var message = await messageFactoryAsync.Invoke(context, ct);
            var services = ConsumerAmbient.Services;
            var publisher = services.GetRequiredService<IPublisher>();
            await publisher.PublishAsync(message, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> Response<T>(
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

    public IFlowOperator<TInstance, TMessage> ResponseAsync<T>(
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

    public IFlowOperator<TInstance, TMessage> Schedule<T>([NotNull] ISchedule<T> schedule,
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

    public IFlowOperator<TInstance, TMessage> ScheduleAsync<T>([NotNull] ISchedule<T> schedule,
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
            var publisher = ConsumerAmbient.Services.GetRequiredService<IPublisher>();
            var tokenId = Guid.NewGuid();
            var setter = scheduleConfigurator.TokenIdProvider.GetSetter();
            setter.Invoke(context.Instance, tokenId);
            await publisher.PublishAsync(message,
                new PublisherContext(context)
                    { Delay = delay, ScheduledMessageId = tokenId, MessageKey = schedule.Received.Name }, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> Unschedule<T>([NotNull] ISchedule<T> schedule) where T : class
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            if (!stateMachine.InternalActivityConfigurators.TryGetValue(schedule, out var configurator) ||
                configurator is not IScheduleConfigurator<TInstance, T> scheduleConfigurator) return;
            var publisher = ConsumerAmbient.Services.GetRequiredService<IPublisher>();
            var setter = scheduleConfigurator.TokenIdProvider.GetSetter();
            var tokenId = scheduleConfigurator.TokenIdProvider.Compile().Invoke(context.Instance);
            await publisher.PublishAsync(new DiscardMessagePublished<T>(tokenId),
                new PublisherContext(context) { MessageKey = schedule.Received.Name }, ct);
            setter.Invoke(context.Instance, null); // Set the token Id to null
        }
    }

    public IFlowOperator<TInstance, TMessage> Request<TRequest, TResponse>(IRequest<TRequest, TResponse> request,
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

    public IFlowOperator<TInstance, TMessage> RequestAsync<TRequest, TResponse>(IRequest<TRequest, TResponse> request,
        MessageOperatorFactoryAsync<TInstance, TMessage, TRequest> messageFactoryAsync)
        where TRequest : class where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(messageFactoryAsync);
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            if (!stateMachine.InternalActivityConfigurators.TryGetValue(request, out var configurator) ||
                configurator is not IRequestConfigurator<TInstance, TRequest, TResponse> scheduleConfigurator) return;
            var message = await messageFactoryAsync.Invoke(context, ct);
            // in-memory process message based on request/reply pattern
            // run in background without current process
            // need to create a new service scope before it is disposed!
            var newScope = ConsumerAmbient.Services.CreateScope();
            _ = Task.Run(async () =>
            {
                var services = newScope.ServiceProvider;
                var requester = services.GetRequiredService<IRequester<TRequest>>();
                var timeout = scheduleConfigurator.Timeout;
                var ttl = scheduleConfigurator.TimeToLive;

                var requestContext = new RequestContext(context.CorrelationId, context.Headers)
                {
                    Timeout = timeout, TimeToLive = ttl
                };
                // Re-check this one, if we resolve IServer or the stateMachine directly? Or we have some other specification ways
                try
                {
                    var responseContext = await requester.RequestAsync<TResponse>(message, requestContext, ct);
                    var server = services.GetRequiredService<IServer<TResponse>>();
                    await server.ConsumeAsync(new StateMachineConsumerContext<TResponse>(responseContext.Message,
                        responseContext.RequesterId, responseContext) { MessageKey = request.Completed.Name }, ct);
                }
                catch (TimeoutException)
                {
                    // Need to invoke timeout event
                    var server = services.GetRequiredService<IServer<RequestTimeoutExpired<TRequest>>>();
                    var timeoutResponse = new RequestTimeoutExpired<TRequest>(message, context.CorrelationId,
                        DateTime.UtcNow.Add(timeout),
                        requestContext.RequesterId);
                    await server.ConsumeAsync(new StateMachineConsumerContext<RequestTimeoutExpired<TRequest>>(
                        timeoutResponse,
                        requestContext.RequesterId, context), ct);
                }
                catch (Exception e)
                {
                    // Need to invoke fault event
                    var server = services.GetRequiredService<IServer<Fault<TRequest>>>();
                    var faultResponse = new Fault<TRequest>(message).FromException(e);
                    await server.ConsumeAsync(new StateMachineConsumerContext<Fault<TRequest>>(faultResponse,
                        requestContext.RequesterId, context), ct);
                }
                finally
                {
                    newScope.Dispose();
                }
            }, ct);
        }
    }

    public IFlowOperator<TInstance, TMessage> Activity(StateMchineOperatorActivity<TInstance, TMessage> activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return ThenAsync(ActionAsync);

        async Task ActionAsync(IStateMachineContext<TInstance, TMessage> context, CancellationToken ct)
        {
            var stateMachineActivityConfigurator = new StateMachineActivityConfigurator<TInstance, TMessage>();
            activity.Invoke(stateMachineActivityConfigurator);
            var stateMachineActivityType = stateMachineActivityConfigurator.StateMachineActivityType;
            var services = ConsumerAmbient.Services;
            var stateMachineActity = services
                .GetRequiredKeyedService<IStateMachineActivity<TInstance, TMessage>>(stateMachineActivityType);
            try
            {
                await stateMachineActity.ExecuteAsync(context, ct);
            }
            catch (Exception e)
            {
                await stateMachineActity.FaultedAsync(
                    new StateMachineContext<TInstance, TMessage>(context.Instance, context.Message,
                        context.RequesterId, context), e, ct);
            }
        }
    }
}