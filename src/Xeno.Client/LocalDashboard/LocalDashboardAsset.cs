namespace Xeno.Client.LocalDashboard
{
    /// <summary>
    /// Local dashboard asset.
    /// </summary>
    public class LocalDashboardAsset
    {
        /// <summary>
        /// Asset bytes.
        /// </summary>
        public byte[] Bytes { get; set; } = System.Array.Empty<byte>();

        /// <summary>
        /// Content type.
        /// </summary>
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>
        /// Cache control header.
        /// </summary>
        public string CacheControl { get; set; } = "no-store";
    }
}
