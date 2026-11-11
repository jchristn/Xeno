namespace Xeno.Core.Analytics
{
    using System;

    /// <summary>
    /// One minute's count of a tenant's clients.
    /// </summary>
    public sealed class ConnectedClientSample
    {
        #region Public-Members

        /// <summary>
        /// Tenant the count covers.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// When the count was taken.
        /// </summary>
        public DateTime SampledUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Clients connected at that moment.
        /// </summary>
        public int Connected { get; set; } = 0;

        /// <summary>
        /// Clients registered at that moment.
        /// </summary>
        public int Total { get; set; } = 0;

        #endregion
    }
}
