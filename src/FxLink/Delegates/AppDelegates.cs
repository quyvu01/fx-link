namespace FxLink.Delegates;

public delegate Task PublisherHandlerDelegate(CancellationToken token = default);

public delegate Task ConsumerHandlerDelegate(CancellationToken token = default);