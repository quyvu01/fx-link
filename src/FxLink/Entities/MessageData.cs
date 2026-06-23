using FxLink.Abstractions;

namespace FxLink.Entities;

public sealed record MessageData<TMessage>(TMessage Message, IContext Context, CancellationToken Token)
    where TMessage : class;