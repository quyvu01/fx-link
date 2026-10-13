using FxLink.Abstractions;
using FxLink.Supervision;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Supervision;

// Ported from FxMap's test/FxMap.Tests/UnitTests/Supervision/ServerSupervisorTests.cs (see
// docs/lessons-from-fxmap.md item 1) and adapted to FxLink's IMessageBrokerConnector/ServerSupervisor.
// No real broker involved — FakeServer below stands in for a transport connector, with its
// per-start behavior driven entirely by the start attempt count (and, for the two race tests,
// a controllable "slow to notice cancellation" delay) so every test is deterministic rather
// than relying on repeated runs to catch a timing-dependent bug.
public class ServerSupervisorTests
{
    private enum Outcome
    {
        FailImmediately,
        RunForeverThenCancel,
        RunThenFail
    }

    private sealed class FakeServer(Func<int, (Outcome Outcome, TimeSpan Extra)> plan) : IMessageBrokerConnector
    {
        private int _startCount;
        private int _stopCount;
        public int StartCount => _startCount;
        public int StopCount => _stopCount;

        public static FakeServer AlwaysHealthy() => new(_ => (Outcome.RunForeverThenCancel, TimeSpan.Zero));

        public static FakeServer AlwaysFails() => new(_ => (Outcome.FailImmediately, TimeSpan.Zero));

        public static FakeServer FailsOnFirstNStarts(int n) => new(count =>
            count <= n ? (Outcome.FailImmediately, TimeSpan.Zero) : (Outcome.RunForeverThenCancel, TimeSpan.Zero));

        // Simulates a connector whose teardown (closing sockets/channels) takes real time once its
        // token is cancelled — used to deterministically reproduce the race the original Supervisor
        // bug depended on (state.Cts already replaced by a restart before the old run's catch filter
        // gets to read it).
        public static FakeServer SlowToUnwindOnce(TimeSpan unwindDelay) =>
            new(_ => (Outcome.RunForeverThenCancel, unwindDelay));

        public static FakeServer HealthyForAWhileThenFails(TimeSpan runFor) =>
            new(_ => (Outcome.RunThenFail, runFor));

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var count = Interlocked.Increment(ref _startCount);
            var (outcome, extra) = plan(count);

            switch (outcome)
            {
                case Outcome.FailImmediately:
                    throw new InvalidOperationException($"boom on start #{count}");

                case Outcome.RunThenFail:
                    await Task.Delay(extra, CancellationToken.None);
                    throw new InvalidOperationException($"boom after running on start #{count}");

                default:
                    try
                    {
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        if (extra > TimeSpan.Zero) await Task.Delay(extra, CancellationToken.None);
                        throw;
                    }

                    break;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _stopCount);
            return Task.CompletedTask;
        }
    }

    private sealed class CustomException : Exception;

    private static SupervisorOptions FastOptions(SupervisionStrategy strategy = SupervisionStrategy.OneForOne) => new()
    {
        Strategy = strategy,
        InitialBackoff = TimeSpan.FromMilliseconds(5),
        MaxBackoff = TimeSpan.FromMilliseconds(25),
        BackoffMultiplier = 2.0,
        MaxRestarts = 32,
        EnableCircuitBreaker = false
    };

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task A_failed_server_is_restarted_and_becomes_healthy_again()
    {
        var server = FakeServer.FailsOnFirstNStarts(1);
        var supervisor = new ServerSupervisor(FastOptions());
        supervisor.RegisterServer("a", server);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.Healthy
                                        && server.StartCount == 2);

            var state = supervisor.GetServerStates()["a"];
            state.RestartCount.ShouldBe(1);
            state.Health.ShouldBe(ServerHealth.Healthy);
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    [Fact]
    public async Task Backoff_doubles_after_each_restart_up_to_the_maximum()
    {
        var server = FakeServer.FailsOnFirstNStarts(3);
        var options = FastOptions();
        var supervisor = new ServerSupervisor(options);
        supervisor.RegisterServer("a", server);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.Healthy
                                        && server.StartCount == 4);

            var state = supervisor.GetServerStates()["a"];
            state.RestartCount.ShouldBe(3);
            // InitialBackoff(5) -> 10 -> capped at MaxBackoff(25) from the third restart onward.
            state.CurrentBackoff.ShouldBe(options.MaxBackoff);
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    [Fact]
    public async Task Exceeding_max_restarts_stops_the_server_permanently()
    {
        var server = FakeServer.AlwaysFails();
        var options = FastOptions();
        options.MaxRestarts = 2;
        var supervisor = new ServerSupervisor(options);
        supervisor.RegisterServer("a", server);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.Stopped);

            var state = supervisor.GetServerStates()["a"];
            state.RestartCount.ShouldBe(3); // 1, 2 restart (allowed), 3rd exceeds MaxRestarts -> Stopped.
            server.StartCount.ShouldBe(3);
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    [Fact]
    public async Task Circuit_breaker_opens_after_consecutive_failures_reach_the_threshold()
    {
        var server = FakeServer.AlwaysFails();
        var options = FastOptions();
        options.EnableCircuitBreaker = true;
        options.CircuitBreakerThreshold = 2;
        options.CircuitBreakerResetTime = TimeSpan.FromSeconds(30); // long enough not to fire in this test
        var supervisor = new ServerSupervisor(options);
        supervisor.RegisterServer("a", server);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.CircuitOpen);

            var state = supervisor.GetServerStates()["a"];
            state.ConsecutiveFailures.ShouldBe(2);
            server.StartCount.ShouldBe(2); // No further restart while the circuit stays open.
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    [Fact]
    public async Task Circuit_breaker_resets_and_restarts_after_the_reset_time()
    {
        var server = FakeServer.FailsOnFirstNStarts(2);
        var options = FastOptions();
        options.EnableCircuitBreaker = true;
        options.CircuitBreakerThreshold = 2;
        options.CircuitBreakerResetTime = TimeSpan.FromMilliseconds(30);
        var supervisor = new ServerSupervisor(options);
        supervisor.RegisterServer("a", server);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.CircuitOpen);
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.Healthy
                                        && server.StartCount == 3);

            supervisor.GetServerStates()["a"].ConsecutiveFailures.ShouldBe(0);
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    [Fact]
    public async Task Resume_directive_does_not_restart_the_server()
    {
        var server = FakeServer.FailsOnFirstNStarts(1);
        var options = FastOptions();
        options.ExceptionDirectives[typeof(InvalidOperationException)] = SupervisorDirective.Resume;
        var supervisor = new ServerSupervisor(options);
        supervisor.RegisterServer("a", server);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].ConsecutiveFailures == 1);
            await Task.Delay(100); // give a would-be restart a chance to happen

            server.StartCount.ShouldBe(1); // Resume never restarts.
            supervisor.GetServerStates()["a"].RestartCount.ShouldBe(0);
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    [Fact]
    public async Task Escalate_directive_stops_the_server_permanently_without_restarting()
    {
        var server = FakeServer.AlwaysFails();
        var options = FastOptions();
        options.ExceptionDirectives[typeof(InvalidOperationException)] = SupervisorDirective.Escalate;
        var supervisor = new ServerSupervisor(options);
        supervisor.RegisterServer("a", server);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.Stopped);
            await Task.Delay(50);

            server.StartCount.ShouldBe(1); // Escalate never restarts either.
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    [Fact]
    public async Task OneForOne_restarts_only_the_failed_server()
    {
        var failing = FakeServer.FailsOnFirstNStarts(1);
        var healthy = FakeServer.AlwaysHealthy();
        var supervisor = new ServerSupervisor(FastOptions(SupervisionStrategy.OneForOne));
        supervisor.RegisterServer("failing", failing);
        supervisor.RegisterServer("healthy", healthy);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["failing"].Health == ServerHealth.Healthy
                                        && failing.StartCount == 2);
            await Task.Delay(50); // let anything that would wrongly touch "healthy" have a chance to run

            healthy.StartCount.ShouldBe(1);
            healthy.StopCount.ShouldBe(0);
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    // Regression test for the Supervisor fix: StartServer used to read state.Cts (not a locally
    // captured copy) in its cancellation-vs-failure filter. OneForAll's restart path cancels every
    // server's old Cts and then immediately assigns each a brand-new one; if the old run's own
    // catch filter unwinds after that reassignment, it used to read the NEW (non-cancelled) source
    // and treat the ordinary cancellation as a real failure, marking a perfectly healthy server
    // "Stopped". SlowToUnwindOnce makes that ordering deterministic instead of a 4-in-6-runs flake.
    [Fact]
    public async Task OneForAll_restarts_every_server_once_without_a_cascade()
    {
        var unwindDelay = TimeSpan.FromMilliseconds(150);
        var failing = FakeServer.FailsOnFirstNStarts(1);
        var slowToUnwind = FakeServer.SlowToUnwindOnce(unwindDelay);
        var supervisor = new ServerSupervisor(FastOptions(SupervisionStrategy.OneForAll));
        supervisor.RegisterServer("failing", failing);
        supervisor.RegisterServer("slow", slowToUnwind);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => failing.StartCount == 2 && slowToUnwind.StartCount == 2,
                TimeSpan.FromSeconds(5));

            // The bug this guards against only manifests once the old "slow" run's delayed unwind
            // finishes and its (misclassified) failure handling overwrites Health — which briefly
            // looks Healthy beforehand. Waiting past the unwind delay before asserting is the point
            // of this test; asserting on the first momentary "Healthy" would miss the regression.
            await Task.Delay(unwindDelay + TimeSpan.FromMilliseconds(150));

            // Neither server should have been restarted more than once (no cascade), and crucially
            // "slow" must never have been marked Stopped by the misclassified cancellation.
            failing.StartCount.ShouldBe(2);
            slowToUnwind.StartCount.ShouldBe(2);
            supervisor.GetServerStates()["failing"].Health.ShouldBe(ServerHealth.Healthy);
            supervisor.GetServerStates()["slow"].Health.ShouldBe(ServerHealth.Healthy);
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    // Regression test for the ordering fix: RestForOne used to read _servers.Values.ToList(), whose
    // order is not registration order, to find "the servers after the failed one".
    [Fact]
    public async Task RestForOne_restarts_the_failed_server_and_the_ones_registered_after_it_only()
    {
        var before = FakeServer.AlwaysHealthy();
        var failing = FakeServer.FailsOnFirstNStarts(1);
        var after = FakeServer.AlwaysHealthy();
        var supervisor = new ServerSupervisor(FastOptions(SupervisionStrategy.RestForOne));
        supervisor.RegisterServer("before", before);
        supervisor.RegisterServer("failing", failing);
        supervisor.RegisterServer("after", after);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["failing"].Health == ServerHealth.Healthy
                                        && failing.StartCount == 2 && after.StartCount == 2);
            await Task.Delay(50);

            before.StartCount.ShouldBe(1);
            before.StopCount.ShouldBe(0);
            after.StartCount.ShouldBe(2); // Restarted alongside "failing" even though it didn't fail.
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    [Fact]
    public async Task ServerHealthChanged_event_reports_the_previous_and_new_health()
    {
        var server = FakeServer.FailsOnFirstNStarts(1);
        var supervisor = new ServerSupervisor(FastOptions());
        supervisor.RegisterServer("a", server);
        var transitions = new List<(ServerHealth Previous, ServerHealth New)>();
        supervisor.ServerHealthChanged += (_, args) => transitions.Add((args.PreviousHealth, args.NewHealth));

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.Healthy
                                        && server.StartCount == 2);

            transitions.ShouldContain((ServerHealth.Healthy, ServerHealth.Degraded));
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_cancels_and_stops_every_running_server()
    {
        var a = FakeServer.AlwaysHealthy();
        var b = FakeServer.AlwaysHealthy();
        var supervisor = new ServerSupervisor(FastOptions());
        supervisor.RegisterServer("a", a);
        supervisor.RegisterServer("b", b);

        await supervisor.StartAsync();
        await WaitUntilAsync(() => a.StartCount == 1 && b.StartCount == 1);

        await supervisor.StopAsync();

        a.StopCount.ShouldBe(1);
        b.StopCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_stops_everything_even_with_the_circuit_open()
    {
        var server = FakeServer.AlwaysFails();
        var options = FastOptions();
        options.EnableCircuitBreaker = true;
        options.CircuitBreakerThreshold = 1;
        options.CircuitBreakerResetTime = TimeSpan.FromSeconds(30);
        var supervisor = new ServerSupervisor(options);
        supervisor.RegisterServer("a", server);

        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.CircuitOpen);

        var disposeTask = supervisor.DisposeAsync().AsTask();
        var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBe(disposeTask);
    }

    [Fact]
    public async Task Failures_far_apart_do_not_accumulate_toward_the_restart_limit()
    {
        var server = new FakeServer(count => count switch
        {
            1 => (Outcome.FailImmediately, TimeSpan.Zero),
            2 => (Outcome.RunThenFail, TimeSpan.FromMilliseconds(80)),
            _ => (Outcome.RunForeverThenCancel, TimeSpan.Zero)
        });
        var options = FastOptions();
        options.MaxRestarts = 1;
        options.MaxRestartWindow = TimeSpan.FromMilliseconds(30); // shorter than the 80ms healthy stretch above
        var supervisor = new ServerSupervisor(options);
        supervisor.RegisterServer("a", server);

        await supervisor.StartAsync();
        try
        {
            await WaitUntilAsync(() => supervisor.GetServerStates()["a"].Health == ServerHealth.Healthy
                                        && server.StartCount == 3, TimeSpan.FromSeconds(5));

            // If the far-apart failures had wrongly accumulated, RestartCount would be 2 here and
            // MaxRestarts(1) would have stopped the server permanently instead.
            supervisor.GetServerStates()["a"].RestartCount.ShouldBe(1);
        }
        finally
        {
            await supervisor.StopAsync();
        }
    }
}
