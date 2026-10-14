namespace Xeno.Client
{
    using System;

    internal sealed class ClientPingResponse
    {
        public string Status { get; set; } = String.Empty;

        public string ServerUtc { get; set; } = String.Empty;
    }
}
