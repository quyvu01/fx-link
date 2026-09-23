namespace FxLink.Aws.Sqs.Registries;

public sealed class SqsCredential : ISqsCredential
{
    internal string AccessKeyIdValue { get; private set; }
    internal string SecretAccessKeyValue { get; private set; }
    internal string ServiceUrlValue { get; private set; }

    public void AccessKeyId(string accessKeyId) => AccessKeyIdValue = accessKeyId;
    public void SecretAccessKey(string secretAccessKey) => SecretAccessKeyValue = secretAccessKey;
    public void ServiceUrl(string serviceUrl) => ServiceUrlValue = serviceUrl;
}