using FxLink.Abstractions;
using FxLink.Supervision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FxLink.Aws.Sqs.BackgroundServices;

internal sealed class SqsSupervisorWorker(
    IMessageBrokerConnector connector,
    ILogger<SqsSupervisorWorker> logger,
    IServiceProvider serviceProvider,
    ISupervisorOptions options)
    : BackgroundService
{
    private ServerSupervisor _supervisor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _supervisor = new ServerSupervisor(options, serviceProvider.GetService<ILogger<ServerSupervisor>>());

        // Subscribe to health changes for logging
        _supervisor.ServerHealthChanged += OnServerHealthChanged;

        try
        {
            // Register the single Aws.Sqs server
            _supervisor.RegisterServer("Aws.Sqs", connector);

            // Start the supervisor
            await _supervisor.StartAsync(stoppingToken);

            // Wait until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
            logger.LogInformation("Aws.Sqs Supervisor shutting down...");
        }
        finally
        {
            await _supervisor.StopAsync(CancellationToken.None);
            _supervisor.ServerHealthChanged -= OnServerHealthChanged;
            await _supervisor.DisposeAsync();
        }
    }

    private void OnServerHealthChanged(object sender, ServerHealthChangedEventArgs e)
    {
        var logLevel = e.NewHealth switch
        {
            ServerHealth.Healthy => LogLevel.Information,
            ServerHealth.Degraded => LogLevel.Warning,
            ServerHealth.Unhealthy => LogLevel.Warning,
            ServerHealth.CircuitOpen => LogLevel.Error,
            ServerHealth.Stopped => LogLevel.Critical,
            _ => LogLevel.Information
        };

        logger.Log(logLevel, e.Exception, "Aws.Sqs Server {ServerId} health: {Previous} -> {New}",
            e.ServerId, e.PreviousHealth, e.NewHealth);
    }
}