namespace Xeno.Client.LocalDashboard.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Local dashboard log response.
    /// </summary>
    public class LocalDashboardLogResponse
    {
        /// <summary>
        /// Log file path.
        /// </summary>
        public string LogFile { get; set; } = string.Empty;

        /// <summary>
        /// Returned log lines.
        /// </summary>
        public List<string> Lines { get; set; } = new List<string>();
    }
}
