using Amazon;

namespace FxLink.Aws.Sqs.Registries;

internal sealed class SqsConfigurator : ISqsConfigurator
{
    internal RegionEndpoint AwsRegionValue { get; private set; }
    internal ISqsCredential Credential { get; } = new SqsCredential();
    internal int MaxReceiveCountValue { get; private set; } = 5;

    public void Region(RegionEndpoint region, Action<ISqsCredential> configure = null)
    {
        AwsRegionValue = region;
        configure?.Invoke(Credential);
    }

    public void MaxReceiveCount(int maxReceiveCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxReceiveCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxReceiveCount, 1000); // SQS RedrivePolicy's own limit
        MaxReceiveCountValue = maxReceiveCount;
    }
}
