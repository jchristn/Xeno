namespace Xeno.Client.LocalDashboard.Models
{
    using System;

    /// <summary>
    /// Local dashboard database response.
    /// </summary>
    public class LocalDashboardDatabaseResponse
    {
        /// <summary>
        /// Database status.
        /// </summary>
        public LocalDashboardDatabaseStatusEnum Status { get; set; } = LocalDashboardDatabaseStatusEnum.Unknown;

        /// <summary>
        /// Configured filename.
        /// </summary>
        public string Filename { get; set; } = string.Empty;

        /// <summary>
        /// Resolved filename.
        /// </summary>
        public string ResolvedFilename { get; set; } = string.Empty;

        /// <summary>
        /// Whether the file exists.
        /// </summary>
        public bool Exists { get; set; } = false;

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long SizeBytes { get; set; } = 0;

        /// <summary>
        /// Request count.
        /// </summary>
        public int Requests { get; set; } = 0;

        /// <summary>
        /// Oldest request timestamp.
        /// </summary>
        public DateTime? OldestRequestUtc { get; set; } = null;

        /// <summary>
        /// Newest request timestamp.
        /// </summary>
        public DateTime? NewestRequestUtc { get; set; } = null;
    }
}
