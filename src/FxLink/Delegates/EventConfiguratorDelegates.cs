using FxLink.Contexts;

namespace FxLink.Delegates;

public delegate Task MissingInstanceActionAsync<in TMessage>(IConsumeContext<TMessage> context,
    CancellationToken token = default) where TMessage : class;

public delegate void MissingInstanceAction<in TMessage>(IConsumeContext<TMessage> context) where TMessage : class;
