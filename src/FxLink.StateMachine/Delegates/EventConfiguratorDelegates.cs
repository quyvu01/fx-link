using FxLink.Abstractions.Contexts;

namespace FxLink.StateMachine.Delegates;

public delegate Task MissingInstanceActionAsync<in TMessage>(IConsumerContext<TMessage> context,
    CancellationToken token = default) where TMessage : class;

public delegate void MissingInstanceAction<in TMessage>(IConsumerContext<TMessage> context) where TMessage : class;