using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Wrappers;

public sealed record FxLinkRegistryWrapper(IServiceCollection ServiceCollection);