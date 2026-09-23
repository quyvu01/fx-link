using Amazon;

namespace FxLink.Aws.Sqs.Registries;

public interface ISqsConfigurator
{
    void Region(RegionEndpoint region, Action<ISqsCredential> configure = null);
}