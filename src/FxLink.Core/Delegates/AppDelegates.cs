namespace FxLink.Core.Delegates;

public delegate Task PublisherHandlerDelegate(CancellationToken token = default);

public delegate Task ConsumerHandlerDelegate(CancellationToken token = default);