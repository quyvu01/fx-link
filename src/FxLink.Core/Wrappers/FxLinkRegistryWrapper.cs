using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.Wrappers;

public sealed record FxLinkRegistryWrapper(IServiceCollection ServiceCollection);