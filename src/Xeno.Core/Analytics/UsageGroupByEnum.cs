namespace Xeno.Core.Analytics
{
    /// <summary>
    /// What usage activity is grouped by.
    /// </summary>
    public enum UsageGroupByEnum
    {
        /// <summary>
        /// Model name.
        /// </summary>
        Model,

        /// <summary>
        /// Provider or client that answered.
        /// </summary>
        Provider,

        /// <summary>
        /// Route: local or network.
        /// </summary>
        Route,

        /// <summary>
        /// Xeno client (server only).
        /// </summary>
        Client,

        /// <summary>
        /// Tenant (server, global administrators only).
        /// </summary>
        Tenant,

        /// <summary>
        /// User (server, tenant administrators and above).
        /// </summary>
        User,

        /// <summary>
        /// Credential (server, tenant administrators and above).
        /// </summary>
        Credential
    }
}
