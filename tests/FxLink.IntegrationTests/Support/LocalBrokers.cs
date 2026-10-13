using System.Net.Sockets;

namespace FxLink.IntegrationTests.Support;

// These tests exercise real local brokers rather than mocks/in-memory fakes (see
// docs/lessons-from-fxmap.md section 4) — RabbitMQ/NATS/LocalStack containers that run on this dev
// machine (see the project README) but don't exist on a bare CI runner yet. Every test checks
// reachability first and skips (rather than fails or hangs) when a broker isn't there, the same
// honesty-over-fake-coverage approach already used for the Azure Service Bus transport elsewhere
// in this repo.
public static class LocalBrokers
{
    public static async Task<bool> IsReachableAsync(string host, int port, TimeSpan? timeout = null)
    {
        using var client = new TcpClient();
        try
        {
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeout ?? TimeSpan.FromSeconds(2)));
            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
