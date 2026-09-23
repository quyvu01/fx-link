namespace FxLink.Aws.Sqs.Registries;

public interface ISqsCredential
{
    void AccessKeyId(string accessKeyId);
    void SecretAccessKey(string secretAccessKey);
    void ServiceUrl(string serviceUrl);
}