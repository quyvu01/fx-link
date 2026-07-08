namespace FxLink.RabbitMq.Registries;

public interface IRabbitMqConfigurator
{
    void Host(string host, string virtualHost, int port = 5672, Action<RabbitMqCredential> configure = null);
}