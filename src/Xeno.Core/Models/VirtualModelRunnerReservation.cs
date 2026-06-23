namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// VMR reservation.
    /// </summary>
    public class VirtualModelRunnerReservation
    {
        /// <summary>
        /// Reservation ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("vmrr_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// VMR ID.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = String.Empty;

        /// <summary>
        /// Reservation name.
        /// </summary>
        public string Name { get; set; } = "reservation";

        /// <summary>
        /// Start timestamp.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow.AddHours(1);

        /// <summary>
        /// End timestamp.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow.AddHours(2);

        /// <summary>
        /// Subject IDs.
        /// </summary>
        public List<string> SubjectIds { get; set; } = new List<string>();

        /// <summary>
        /// Whether reservation is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}
