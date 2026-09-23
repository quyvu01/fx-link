using Amazon;

namespace FxLink.Aws.Sqs.Registries;

internal sealed class SqsConfigurator : ISqsConfigurator
{
    internal RegionEndpoint AwsRegionValue { get; private set; }
    internal ISqsCredential Credential { get; } = new SqsCredential();

    public void Region(RegionEndpoint region, Action<ISqsCredential> configure = null)
    {
        AwsRegionValue = region;
        configure?.Invoke(Credential);
    }
}