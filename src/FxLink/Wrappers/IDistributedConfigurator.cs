using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Wrappers;

public interface IDistributedConfigurator
{
    IServiceCollection Services { get; }
}