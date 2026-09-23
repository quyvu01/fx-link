using Amazon;

namespace FxLink.Aws.Sqs.Registries;

internal sealed class SqsConfiguration(
    string awsAccessKeyId,
    string awsSecretAccessKey,
    RegionEndpoint awsRegion,
    string serviceUrl,
    int maxReceiveCount)
    : ISqsConfiguration
{
    public string AwsAccessKeyId { get; } = awsAccessKeyId;
    public string AwsSecretAccessKey { get; } = awsSecretAccessKey;
    public RegionEndpoint AwsRegion { get; } = awsRegion;
    public string ServiceUrl { get; } = serviceUrl;
    public int MaxReceiveCount { get; } = maxReceiveCount;
}
