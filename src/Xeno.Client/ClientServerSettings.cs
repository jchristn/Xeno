namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Client server connection settings.
    /// </summary>
    public class ClientServerSettings
    {
        /// <summary>
        /// Server URL.
        /// </summary>
        public string Url { get; set; } = "ws://app.xenocloud.ai:9000/ws/v1/clients";

        /// <summary>
        /// REST API base URL.
        /// </summary>
        public string? RestUrl { get; set; } = null;

        /// <summary>
        /// Access key.
        /// </summary>
        public string? AccessKey { get; set; } = null;

        /// <summary>
        /// Legacy secret key. This value is not sent to the server.
        /// </summary>
        public string? SecretKey { get; set; } = null;

        /// <summary>
        /// Bearer token.
        /// </summary>
        public string? Token { get; set; } = null;

    }
}
