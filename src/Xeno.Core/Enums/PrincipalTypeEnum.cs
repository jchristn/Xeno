namespace Xeno.Core.Enums
{

    /// <summary>
    /// Authenticated principal type.
    /// </summary>
    public enum PrincipalTypeEnum
    {
        /// <summary>
        /// No principal.
        /// </summary>
        None = 0,

        /// <summary>
        /// User principal.
        /// </summary>
        User = 1,

        /// <summary>
        /// Credential principal.
        /// </summary>
        Credential = 2,

        /// <summary>
        /// System administrator principal.
        /// </summary>
        Administrator = 3
    }
}
