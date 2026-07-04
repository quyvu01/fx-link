using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;

namespace FxLink.Delegates;

public delegate Task PublisherHandlerDelegate(CancellationToken token = default);

public delegate Task ConsumerHandlerDelegate(CancellationToken token = default);

public delegate Task DispatchHandlerDelegate(IContext context, CancellationToken token = default);