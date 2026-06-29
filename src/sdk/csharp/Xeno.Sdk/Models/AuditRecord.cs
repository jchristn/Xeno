namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno audit record DTO.
    /// </summary>
    public class AuditRecord
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the tenant id.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the reason code.
        /// </summary>
        public string ReasonCode { get; set; } = String.Empty;
    }
}
