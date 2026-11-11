namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of file_metadata.
    /// </summary>
    public sealed class FileMetadataResult
    {
        /// <summary>
        /// True when the metadata was read.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        /// <summary>
        /// Path, relative to the working directory.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// file or directory.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = String.Empty;

        /// <summary>
        /// File size in bytes; files only.
        /// </summary>
        [JsonPropertyName("size_bytes")]
        public long? SizeBytes { get; set; } = null;

        /// <summary>
        /// Creation time, ISO 8601 UTC.
        /// </summary>
        [JsonPropertyName("created_utc")]
        public string CreatedUtc { get; set; } = String.Empty;

        /// <summary>
        /// Last write time, ISO 8601 UTC.
        /// </summary>
        [JsonPropertyName("modified_utc")]
        public string ModifiedUtc { get; set; } = String.Empty;

        /// <summary>
        /// Last access time, ISO 8601 UTC.
        /// </summary>
        [JsonPropertyName("accessed_utc")]
        public string AccessedUtc { get; set; } = String.Empty;

        /// <summary>
        /// True when the file is read-only; files only.
        /// </summary>
        [JsonPropertyName("is_readonly")]
        public bool? IsReadonly { get; set; } = null;

        /// <summary>
        /// File extension including the dot; files only.
        /// </summary>
        [JsonPropertyName("extension")]
        public string? Extension { get; set; } = null;

        /// <summary>
        /// Files directly inside; directories only.
        /// </summary>
        [JsonPropertyName("file_count")]
        public int? FileCount { get; set; } = null;

        /// <summary>
        /// Directories directly inside; directories only.
        /// </summary>
        [JsonPropertyName("directory_count")]
        public int? DirectoryCount { get; set; } = null;
    }
}
