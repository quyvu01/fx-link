using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Wrappers;

public sealed record RegistryWrapper(IServiceCollection ServiceCollection);