namespace Xeno.Server
{
    using System;
    using System.Threading;
    using WatsonWebserver.Core.WebSockets;

    internal sealed class ClientConnection
    {
        public ClientConnection(string clientId, string tenantId, WebSocketSession session)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public string ClientId { get; private set; }

        public string TenantId { get; private set; }

        public WebSocketSession Session { get; private set; }

        public SemaphoreSlim SendLock { get; private set; } = new SemaphoreSlim(1, 1);
    }
}
