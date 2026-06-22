using FxLink.Core.Abstractions;

namespace FxLink.Core.Entities;

public sealed record MessageData<TMessage>(TMessage Message, IContext Context, CancellationToken Token)
    where TMessage : class;