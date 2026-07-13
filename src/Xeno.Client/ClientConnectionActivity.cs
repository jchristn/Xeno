namespace Xeno.Client
{
    using System;

    internal sealed class ClientConnectionActivity
    {
        private readonly object _Lock = new object();
        private DateTime _LastServerMessageUtc;
        private string? _FailureReason = null;

        public ClientConnectionActivity(DateTime connectedUtc)
        {
            _LastServerMessageUtc = connectedUtc;
        }

        public string? FailureReason
        {
            get
            {
                lock (_Lock)
                {
                    return _FailureReason;
                }
            }
        }

        public void MarkServerMessage(DateTime utcNow)
        {
            lock (_Lock)
            {
                _LastServerMessageUtc = utcNow;
            }
        }

        public bool IsStale(DateTime utcNow, int livenessTimeoutMs)
        {
            lock (_Lock)
            {
                TimeSpan age = utcNow - _LastServerMessageUtc;
                return age > TimeSpan.FromMilliseconds(Math.Max(5000, livenessTimeoutMs));
            }
        }

        public bool TryFail(string reason)
        {
            lock (_Lock)
            {
                if (!String.IsNullOrWhiteSpace(_FailureReason)) return false;
                _FailureReason = reason;
                return true;
            }
        }
    }
}
