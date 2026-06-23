namespace Xeno.Core.Enums
{

    /// <summary>
    /// Reservation gate decision.
    /// </summary>
    public enum ReservationDecisionEnum
    {
        /// <summary>
        /// No active reservation applies.
        /// </summary>
        NoReservation = 0,

        /// <summary>
        /// Reservation gate allowed the request.
        /// </summary>
        Allowed = 1,

        /// <summary>
        /// Reservation gate denied the request.
        /// </summary>
        Denied = 2,

        /// <summary>
        /// Conflicting reservations were found.
        /// </summary>
        Conflict = 3
    }
}
