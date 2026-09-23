using Amazon;

namespace FxLink.Aws.Sqs.Registries;

internal interface ISqsConfiguration
{
    string AwsAccessKeyId { get; }
    string AwsSecretAccessKey { get; }
    RegionEndpoint AwsRegion { get; }
    string ServiceUrl { get; }
}
