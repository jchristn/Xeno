namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// Syslog server target for logging.
    /// </summary>
    public class SyslogServer
    {
        /// <summary>
        /// Hostname of the syslog server.
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// Port of the syslog server. Minimum 0, maximum 65535.
        /// </summary>
        public int Port
        {
            get => _Port;
            set
            {
                if (value < 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        private string _Hostname = "127.0.0.1";
        private int _Port = 514;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SyslogServer()
        {
        }

        /// <summary>
        /// Instantiate with a hostname and port.
        /// </summary>
        /// <param name="hostname">Hostname.</param>
        /// <param name="port">Port.</param>
        public SyslogServer(string hostname, int port)
        {
            Hostname = hostname;
            Port = port;
        }
    }
}
