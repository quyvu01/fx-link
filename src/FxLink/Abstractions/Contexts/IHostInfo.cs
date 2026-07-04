namespace FxLink.Abstractions.Contexts;

public interface IHostInfo
{
    string MachineName { get; }
    string ProcessName { get; }
    int ProcessId { get; }
    string Assembly { get; }
}

/// <summary>
/// Represents host information where the fault occurred.
/// </summary>
public sealed class HostInfo : IHostInfo
{
    /// <summary>
    /// Gets or sets the machine name.
    /// </summary>
    public string MachineName { get; private init; }

    /// <summary>
    /// Gets or sets the process name.
    /// </summary>
    public string ProcessName { get; private init; }

    /// <summary>
    /// Gets or sets the process ID.
    /// </summary>
    public int ProcessId { get; private init; }

    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    public string Assembly { get; private init; }

    /// <summary>
    /// Gets the current host information.
    /// </summary>
    public static HostInfo Current
    {
        get
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            return new HostInfo
            {
                MachineName = Environment.MachineName,
                ProcessName = process.ProcessName,
                ProcessId = process.Id,
                Assembly = AppDomain.CurrentDomain.FriendlyName
            };
        }
    }
}