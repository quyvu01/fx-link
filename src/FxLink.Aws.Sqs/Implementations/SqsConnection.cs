using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using FxLink.Aws.Sqs.Abstractions;
using FxLink.Aws.Sqs.Registries;

namespace FxLink.Aws.Sqs.Implementations;

internal sealed class SqsConnection : ISqsConnection, IDisposable
{
    public AmazonSQSClient Sqs { get; }
    public AmazonSimpleNotificationServiceClient Sns { get; }

    public SqsConnection(ISqsConfiguration sqsConfiguration)
    {
        AWSCredentials credentials = null;
        if (!string.IsNullOrEmpty(sqsConfiguration.AwsAccessKeyId) &&
            !string.IsNullOrEmpty(sqsConfiguration.AwsSecretAccessKey))
        {
            credentials =
                new BasicAWSCredentials(sqsConfiguration.AwsAccessKeyId, sqsConfiguration.AwsSecretAccessKey);
        }

        var region = sqsConfiguration.AwsRegion ?? RegionEndpoint.USEast1;
        var sqsConfig = new AmazonSQSConfig { RegionEndpoint = region };
        var snsConfig = new AmazonSimpleNotificationServiceConfig { RegionEndpoint = region };

        // Support LocalStack for testing
        if (!string.IsNullOrEmpty(sqsConfiguration.ServiceUrl))
        {
            sqsConfig.ServiceURL = sqsConfiguration.ServiceUrl;
            snsConfig.ServiceURL = sqsConfiguration.ServiceUrl;
        }

        Sqs = credentials != null ? new AmazonSQSClient(credentials, sqsConfig) : new AmazonSQSClient(sqsConfig);
        Sns = credentials != null
            ? new AmazonSimpleNotificationServiceClient(credentials, snsConfig)
            : new AmazonSimpleNotificationServiceClient(snsConfig);
    }

    public void Dispose()
    {
        Sqs.Dispose();
        Sns.Dispose();
    }
}
