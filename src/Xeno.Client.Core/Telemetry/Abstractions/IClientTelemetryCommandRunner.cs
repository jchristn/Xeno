namespace Xeno.Client.Telemetry.Abstractions
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Command runner used by telemetry providers.
    /// </summary>
    public interface IClientTelemetryCommandRunner
    {
        /// <summary>
        /// Run a command and return standard output.
        /// </summary>
        /// <param name="filename">Executable filename.</param>
        /// <param name="arguments">Arguments.</param>
        /// <param name="timeout">Timeout.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Command output.</returns>
        Task<string> RunAsync(string filename, string arguments, TimeSpan timeout, CancellationToken token);
    }
}
