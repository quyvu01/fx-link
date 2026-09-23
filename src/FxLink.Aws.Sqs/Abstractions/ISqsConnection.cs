using Amazon.SimpleNotificationService;
using Amazon.SQS;

namespace FxLink.Aws.Sqs.Abstractions;

/// <summary>
/// Owns the single <see cref="AmazonSQSClient"/>/<see cref="AmazonSimpleNotificationServiceClient"/>
/// shared by every SQS transport component (request client, server) in this process. Both clients
/// are thread-safe and pool their own HTTP connections internally, so there is no benefit to each
/// component dialing its own instance.
/// </summary>
internal interface ISqsConnection
{
    AmazonSQSClient Sqs { get; }
    AmazonSimpleNotificationServiceClient Sns { get; }
}
