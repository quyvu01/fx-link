using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Exceptions;
using FxLink.Serialization;

namespace FxLink.Extensions;

/// <summary>
/// Adds the "hydrate from object values" overloads on top of <see cref="IPublisher"/>,
/// <see cref="IResponse"/>, and <see cref="IRequester{TMessage}"/> — each one builds the strongly
/// typed message via <see cref="MessageContractActivator.CreateFrom{TMessage}"/> and delegates to
/// the corresponding typed-message overload.
/// </summary>
public static class MessageContractExtensions
{
    extension(IPublisher publisher)
    {
        public Task PublishAsync<TMessage>(object values, Action<IPublishContext> contextOptions,
            CancellationToken token = default) where TMessage : class =>
            publisher.PublishAsync(MessageContractActivator.CreateFrom<TMessage>(values), contextOptions, token);

        public Task PublishAsync<TMessage>(object values,
            CancellationToken token = default) where TMessage : class =>
            publisher.PublishAsync(MessageContractActivator.CreateFrom<TMessage>(values), token);
    }

    extension(IResponse response)
    {
        public Task ResponseAsync<TResponse>(object values, CancellationToken token = default)
            where TResponse : class =>
            response.ResponseAsync(MessageContractActivator.CreateFrom<TResponse>(values), token);
    }

    private static readonly ConcurrentDictionary<Type, Delegate> RequesterLookup =
        new();

    extension(IRequester requester)
    {
        public Task<IResponseContext<TResponse>> RequestAsync<TResponse>(object values,
            Action<IRequestContext> contextOptions,
            CancellationToken token = default) where TResponse : class
        {
            var invoker = (Func<IRequester, object, Action<IRequestContext>, CancellationToken,
                Task<IResponseContext<TResponse>>>)RequesterLookup.GetOrAdd(requester.GetType(),
                static requesterType =>
                {
                    var messageType = requesterType.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequester<>))
                        .Select(i => i.GetGenericArguments()[0])
                        .FirstOrDefault() ?? throw new FxLinkException.RequesterMustBeGeneric(requesterType);
                    return IRequester.BuildRequestInternalDelegate<TResponse>(messageType);
                });

            return invoker(requester, values, contextOptions, token);
        }

        public Task<IResponseContext<TResponse>> RequestAsync<TResponse>(object values,
            CancellationToken token = default)
            where TResponse : class
            => requester.RequestAsync<TResponse>(values, null, token);

        private static Task<IResponseContext<TResponse>> RequestInternalAsync<TMessage, TResponse>(
            IRequester<TMessage> internalRequester, object values,
            Action<IRequestContext> contextOptions,
            CancellationToken token = default) where TMessage : class where TResponse : class
        {
            var message = MessageContractActivator.CreateFrom<TMessage>(values);
            return internalRequester.RequestAsync<TResponse>(message, contextOptions, token);
        }

        private static Delegate BuildRequestInternalDelegate<TResponse>(Type messageType) where TResponse : class
        {
            var openMethod = typeof(MessageContractExtensions).GetMethod(nameof(RequestInternalAsync),
                BindingFlags.NonPublic | BindingFlags.Static)!;
            var closedMethod = openMethod.MakeGenericMethod(messageType, typeof(TResponse));

            var requesterParam = Expression.Parameter(typeof(IRequester), "requester");
            var valuesParam = Expression.Parameter(typeof(object), "values");
            var contextOptionsParam = Expression.Parameter(typeof(Action<IRequestContext>), "contextOptions");
            var tokenParam = Expression.Parameter(typeof(CancellationToken), "token");

            var typedRequester = Expression.Convert(requesterParam, typeof(IRequester<>).MakeGenericType(messageType));
            var call = Expression.Call(closedMethod, typedRequester, valuesParam, contextOptionsParam, tokenParam);

            return Expression
                .Lambda<Func<IRequester, object, Action<IRequestContext>, CancellationToken,
                    Task<IResponseContext<TResponse>>>>(
                    call, requesterParam, valuesParam, contextOptionsParam, tokenParam)
                .Compile();
        }
    }
}