using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Wrappers;

internal sealed record DistributedConfigurator(IServiceCollection Services) : IDistributedConfigurator;