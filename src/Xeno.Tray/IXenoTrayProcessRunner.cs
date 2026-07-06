namespace Xeno.Tray
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs xeno CLI processes for tray actions.
    /// </summary>
    public interface IXenoTrayProcessRunner
    {
        /// <summary>
        /// Run a process and capture output.
        /// </summary>
        /// <param name="filename">Executable filename.</param>
        /// <param name="arguments">Arguments.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Process result.</returns>
        Task<XenoTrayProcessResult> RunAsync(string filename, IReadOnlyList<string> arguments, int timeoutMs, CancellationToken token);
    }
}
