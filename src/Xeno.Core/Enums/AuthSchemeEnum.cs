namespace Xeno.Core.Enums
{

    /// <summary>
    /// Authentication scheme.
    /// </summary>
    public enum AuthSchemeEnum
    {
        /// <summary>
        /// No authentication scheme.
        /// </summary>
        None = 0,

        /// <summary>
        /// Bearer session token.
        /// </summary>
        Bearer = 1,

        /// <summary>
        /// Direct access key credential.
        /// </summary>
        Credential = 2,

        /// <summary>
        /// User password login bootstrap.
        /// </summary>
        Password = 4
    }
}
