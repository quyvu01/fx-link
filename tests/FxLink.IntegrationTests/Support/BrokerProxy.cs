using System.Net;
using System.Net.Sockets;

namespace FxLink.IntegrationTests.Support;

// A small TCP forwarder between a test and a real broker (see docs/lessons-from-fxmap.md section 4
// — ported from the technique described there, not from any FxMap source file since this suite
// doesn't have access to that sibling repo). Point the transport at 127.0.0.1:<ListenPort> instead
// of the broker directly; Cut() closes every open connection and refuses new ones; Restore() starts
// listening again on the same port. This simulates a real network outage without touching the
// broker container itself, which also means it works unmodified in CI once this suite gets wired
// in there (see the project README).
public sealed class BrokerProxy : IAsyncDisposable
{
    private readonly string _targetHost;
    private readonly int _targetPort;
    private TcpListener _listener;
    private CancellationTokenSource _acceptLoopCts;
    private Task _acceptLoopTask;
    private readonly List<(TcpClient Left, TcpClient Right)> _activePairs = [];
    private readonly object _pairsLock = new();

    public int ListenPort { get; }

    public BrokerProxy(string targetHost, int targetPort, int listenPort)
    {
        _targetHost = targetHost;
        _targetPort = targetPort;
        ListenPort = listenPort;
    }

    public Task StartAsync()
    {
        Restore();
        return Task.CompletedTask;
    }

    // Closes every connection currently flowing through the proxy and stops accepting new ones —
    // simulates the broker becoming completely unreachable.
    public void Cut()
    {
        _acceptLoopCts?.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch
        {
            // Already stopped — fine.
        }

        List<(TcpClient Left, TcpClient Right)> pairs;
        lock (_pairsLock)
        {
            pairs = [.._activePairs];
            _activePairs.Clear();
        }

        foreach (var (left, right) in pairs)
        {
            try
            {
                left.Close();
            }
            catch
            {
                // Best-effort teardown.
            }

            try
            {
                right.Close();
            }
            catch
            {
                // Best-effort teardown.
            }
        }
    }

    // Starts listening again on the same port — new connections (e.g. a client's automatic
    // recovery) succeed again from this point on.
    public void Restore()
    {
        _listener = new TcpListener(IPAddress.Loopback, ListenPort);
        _listener.Start();
        _acceptLoopCts = new CancellationTokenSource();
        _acceptLoopTask = AcceptLoopAsync(_listener, _acceptLoopCts.Token);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient incoming;
                try
                {
                    incoming = await listener.AcceptTcpClientAsync(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }

                _ = PumpAsync(incoming, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PumpAsync(TcpClient incoming, CancellationToken token)
    {
        TcpClient outgoing;
        try
        {
            outgoing = new TcpClient();
            await outgoing.ConnectAsync(_targetHost, _targetPort, token);
        }
        catch
        {
            incoming.Close();
            return;
        }

        lock (_pairsLock) _activePairs.Add((incoming, outgoing));

        var a = PipeAsync(incoming.GetStream(), outgoing.GetStream(), token);
        var b = PipeAsync(outgoing.GetStream(), incoming.GetStream(), token);
        try
        {
            await Task.WhenAny(a, b);
        }
        catch
        {
            // One direction failing (reset, closed) ends the pair below regardless.
        }
        finally
        {
            lock (_pairsLock) _activePairs.RemoveAll(p => p.Left == incoming);
            incoming.Close();
            outgoing.Close();
        }
    }

    private static async Task PipeAsync(Stream from, Stream to, CancellationToken token)
    {
        var buffer = new byte[8192];
        try
        {
            while (true)
            {
                var read = await from.ReadAsync(buffer, token);
                if (read == 0) return;
                await to.WriteAsync(buffer.AsMemory(0, read), token);
            }
        }
        catch
        {
            // Connection reset/closed — the caller tears down both sides.
        }
    }

    public ValueTask DisposeAsync()
    {
        Cut();
        return ValueTask.CompletedTask;
    }
}
